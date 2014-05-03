//
// TCPServer.cs
//
// Author:
//       Evan Reidland <er@evanreidland.com>
//
// Copyright (c) 2014 Evan Reidland
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TaskMaster.Network
{
    public class TCPServer : Server
    {
        private class TCPServerClient : ServerClient
        {
            public const int SLEEP_INTERVAL = 100; //Should be on config.

            public TCPServer OwnerAsTCPServer { get { return Owner as TCPServer; } }
            private TcpClient _client;
            private NetworkStream _stream;
            private BinaryReader _reader;
            private BinaryWriter _writer;

            private Queue<Packet> _pendingPacketsToSend = new Queue<Packet>();

            private void SendLoop()
            {
                bool runninng = true;

                while (runninng)
                {
                    TcpClient client = _client;

                    if (client != null && client.Connected)
                    {
                        try
                        {
                            bool sending = true;
                            while (sending && client.Connected)
                            {
                                Packet packet = null;
                                lock (_pendingPacketsToSend)
                                {
                                    if (_pendingPacketsToSend.Count > 0)
                                        packet = _pendingPacketsToSend.Dequeue();
                                    else
                                        sending = false;
                                }

                                if (packet != null)
                                    packet.WriteToStream(_writer);
                            }

                            Thread.Sleep(SLEEP_INTERVAL);
                        }
                        catch (Exception)
                        {
                            Log.Default.Error("Encountered exception during SendLoop on client {0}. Dropping client.", ID);
                            Stop();
                        }
                    }
                    else
                    {
                        runninng = false;
                        Stop();
                    }
                }
            }

            private void ReceiveLoop()
            {
                bool runninng = true;

                while (runninng)
                {
                    TcpClient client = _client;

                    if (client != null && client.Connected)
                    {
                        try
                        {
                            Packet packet = Packet.ReadFromStream(ID, _reader);
                            Owner.Events.SendQueued(OwnerAsTCPServer.Actions, packet);
                        }
                        catch (Exception)
                        {
                            Log.Default.Error("Encountered exception during ReceiveLoop on client {0}. Dropping client.", ID);
                            Stop();
                        }
                    }
                    else
                    {
                        runninng = false;
                        Stop();
                    }
                }
            }

            public void Start() 
            {
                OwnerAsTCPServer._workers.AddWorker(SendLoop);
                OwnerAsTCPServer._workers.AddWorker(ReceiveLoop);
            }

            public override void Stop ()
            {
                try
                {
                    if (_client != null)
                    {
                        lock (_client)
                        {
                            _client.Close();
                            _client = null;
                        }
                    }
                } catch (Exception e) { Log.Default.Exception(e); }

                OwnerAsTCPServer.RemoveClient(ID);
            }

            public override NetworkError Send (string text, byte[] binary)
            {
                lock (_client)
                {
                    if (_client != null && _client.Connected)
                    {
                        lock (_pendingPacketsToSend)
                            _pendingPacketsToSend.Enqueue(new Packet(Guid.Empty, text, binary));
                        return NetworkError.None;
                    }
                    else
                        return NetworkError.NotConnected;
                }
            }

            public TCPServerClient(TCPServer owner, TcpClient client) : base(owner)
            {
                _client = client;
                _stream = _client.GetStream();
                _reader = new BinaryReader(_stream);
                _writer = new BinaryWriter(_stream);
            }
        }

        private TcpListener _listener;
        private WorkerThreadPool _workers = new WorkerThreadPool();

        private void ListenLoop()
        {
            bool running = true;
            while (running)
            {
                try
                {
                    TcpListener listener = _listener;

                    if (listener != null)
                    {
                        var tcpClient = _listener.AcceptTcpClient();
                        TCPServerClient client = new TCPServerClient(this, tcpClient);
                        AddClient(client);
                        Events.SendQueued(Actions, new ClientConnectedEvent(this, client.ID));
                        client.Start();
                    }
                    else
                        running = false;

                } catch (Exception e) { Log.Default.Exception(e); }
            }
        }

        public override NetworkError Listen (int port)
        {
            if (State == ServerConnectionState.Disconnected)
            {
                try
                {
                    if (_listener == null)
                    {
                        Port = port;
                        _listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
                        _listener.Start();
                        _workers.AddWorker(ListenLoop);
                        return NetworkError.None;
                    }
                }
                catch (Exception e) { Log.Default.Exception(e); }

                return NetworkError.FailedToListen;
            }
            else
                return NetworkError.AlreadyListening;
        }

        public override NetworkError Send (Guid id, string text, byte[] binary)
        {
            var client = GetClient<TCPServerClient>(id);
            if (client != null)
                return client.Send(text, binary);
            else
                return NetworkError.NoSuchClient;
        }

        public override void Stop()
        {
            try
            {
                if (_listener != null)
                {
                    lock (_listener)
                    {
                        _listener.Stop();
                        _listener = null;
                    }
                }

                foreach (var client in GetClients())
                {
                    client.Stop();
                    RemoveClient(client.ID);
                }
            } catch (Exception e) { Log.Default.Exception(e); }

            _workers.Stop();

            State = ServerConnectionState.Disconnected;
        }

        public TCPServer(EventHub hub) : base(hub) {}
    }
}

