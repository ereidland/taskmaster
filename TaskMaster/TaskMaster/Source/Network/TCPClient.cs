//
// TCPClient.cs
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
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace TaskMaster.Network
{
    public class TCPClient : Client
    {
        private TcpClient _client;

        private WorkerThreadPool _workers = new WorkerThreadPool();
        private Queue<Packet> _pendingPacketsToSend = new Queue<Packet>();

        private void SendLoop()
        {
            if (_client != null)
            {
                var stream = _client.GetStream();
                var writer = new BinaryWriter(stream);

                while (_client != null)
                {
                    if (_client.Connected)
                    {
                        try
                        {
                            bool sending = true;
                            while (sending)
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
                                    packet.WriteToStream(writer);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Default.Error("Encountered exception during SendLoop. Disconnecting.");
                            Disconnect();
                        }
                    }
                    else if (State == ConnectionState.Connected)
                    {
                        try
                        {
                            if (_client != null)
                                _client.Close();
                        } catch (Exception e) { Log.Default.Exception(e); }

                        _client = null;
                        State = ConnectionState.Disconnected;
                    }
                }
            }
        }

        private void ReceiveLoop()
        {
            if (_client != null)
            {
                var stream = _client.GetStream();
                var reader = new BinaryReader(stream);

                while (_client != null)
                {
                    if (_client.Connected)
                    {
                        try
                        {
                            Packet packet = Packet.ReadFromStream(Guid.Empty, reader);
                            Events.SendQueued(Actions, packet);
                        }
                        catch (Exception)
                        {
                            Log.Default.Error("Encountered exception during ReceiveLoop. Disconnecting.");
                            Disconnect();
                        }
                    }
                    else if (State == ConnectionState.Connected)
                    {
                        try
                        {
                            if (_client != null)
                                _client.Close();
                        } catch (Exception e) { Log.Default.Exception(e); }

                        _client = null;
                        State = ConnectionState.Disconnected;
                    }
                }
            }
        }

        public override void Disconnect()
        {
            try
            {
                if (_client != null)
                    _client.Close();
            } catch (Exception e) { Log.Default.Exception(e); }

            _client = null;

            _workers.Stop();
        }

        private void DoConnect()
        {
            if (_client == null)
            {
                try
                {
                    _client = new TcpClient();
                    _client.Connect(Address, Port);
                    if (_client.Connected)
                    {
                        State = ConnectionState.Connected;
                        _workers.AddWorker(SendLoop);
                        _workers.AddWorker(ReceiveLoop);
                    }
                    else
                        Log.Default.Error("Failed to connect to {0}:{1}", Address, Port);
                }
                catch (Exception e) { Log.Default.Exception(e); }

                if (_client == null || !_client.Connected)
                    State = ConnectionState.Disconnected;
            }
        }

        public override NetworkError Send (string text, byte[] binary)
        {
            switch (State)
            {
                case ConnectionState.Connected:
                {
                    lock (_pendingPacketsToSend)
                        _pendingPacketsToSend.Enqueue(new Packet(Guid.Empty, text, binary));
                    return NetworkError.None;
                }
                default:
                    return NetworkError.NotConnected;
            }
        }

        public override NetworkError Connect (string address, int port)
        {
            switch (State)
            {
                case ConnectionState.Connected:
                    return NetworkError.AlreadyConnected;
                case ConnectionState.Connecting:
                    return NetworkError.AlreadyConnecting;
                default:
                {
                    Address = address;
                    Port = port;
                    State = ConnectionState.Connecting;
                    _workers.AddWorker(DoConnect);
                    return NetworkError.None;
                }
            }
        }

        public TCPClient(EventHub hub) : base(hub) {}
    }
}

