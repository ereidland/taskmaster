//
// Main.cs
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
using System.Threading;
using TaskMaster;
using TaskMaster.Network;

namespace TaskMasterConsole
{
    class MainClass
    {
        private static Client StartClient(EventHub hub)
        {
            Client client = new TCPClient(hub);
            string address = "127.0.0.1";
            int port = 32014;
            Console.WriteLine(client.Connect(address, port));
            new Thread((t) =>
            {
                while (true)
                {
                    client.ResolveEvents();
                    Thread.Sleep(100);
                }
            }).Start();
            return client;
        }

        private static Server StartServer(EventHub hub)
        {
            Server server = new TCPServer(hub);
            FileListJSONHandler fileListJSONHandler = new FileListJSONHandler();
            JSONProtocol protocol = new JSONProtocol("test.", server);
            fileListJSONHandler.SetupAsHost(protocol, "input/");
            int port = 32014;
            Console.WriteLine(server.Listen(port));
            new Thread((t) =>
            {
                while (true)
                {
                    server.ResolveEvents();
                    Thread.Sleep(100);
                }
            }).Start();
            return server;
        }

        public static void Main(string[] args)
        {
            //TEST for server/client. Replace with actual TaskMaster code.
            Client client = null;
            Server server = null;

            FileListJSONHandler fileListJSONHandler = new FileListJSONHandler();;
            JSONProtocol protocol;
            
            EventHub hub = new EventHub();

            EventHubSubscriber subscriber = new EventHubSubscriber();

            subscriber.AddReceiver<Packet>(hub, (p) =>
            {
                Console.WriteLine("{0}: {1}", p.Sender, p.Text);
            });

            subscriber.AddReceiver<ObjectChangedEvent<ConnectionState>>(hub, (e) =>
            {
                Console.WriteLine("{0} -> {1}", e.OldValue, e.NewValue);
                if (e.NewValue == ConnectionState.Connected)
                {
                    protocol = new JSONProtocol("test.", client);
                    fileListJSONHandler.SetupAsClient(protocol, "output/");
                }
            });

            subscriber.AddReceiver<ClientConnectedEvent>(hub, (e) =>
            {
                Console.WriteLine("New client: " + e.ClientID);
                e.Owner.Send(e.ClientID, "Welcome!", null);
            });

            subscriber.AddReceiver<ClientDisconnectedEvent>(hub, (e) =>
            {
                Console.WriteLine("Client disconnected: " + e.ClientID);
            });

            subscriber.AddReceiver<ObjectChangedEvent<ServerConnectionState>>(hub, (e) =>
            {
                Console.WriteLine("{0} -> {1}", e.OldValue, e.NewValue);
            });

            string str;
            bool running = true;
            while (running && !string.IsNullOrEmpty(str = Console.ReadLine()))
            {
                string toCompare = str.ToLower();
                switch(toCompare)
                {
                    case "/client":
                    {
                        if (client == null)
                            client = StartClient(hub);
                        else
                            Console.WriteLine("Client already started!");

                        break;
                    }
                    case "/server":
                    {
                        if (server == null)
                            server = StartServer(hub);
                        else
                            Console.WriteLine("Server already started!");

                        break;
                    }
                    case "/disconnect":
                    {
                        running = false;
                        Console.WriteLine("As you wish...");
                        break;
                    }
                    default:
                    {
                        if (server != null)
                        {
                            var clients = server.GetClients();
                            if (clients.Count > 0)
                            {
                                foreach (var serverClient in clients)
                                    Console.WriteLine(">{0}: {1}", serverClient.ID, serverClient.Send(str, null));
                            }
                            else
                                Console.WriteLine("No clients to send to!");
                        }
                        else if (client != null)
                        {
                            Console.WriteLine(client.Send(str, null));
                        }
                        else Console.WriteLine("Can't send anything if you're not connected!");
                        break;
                    }
                }
            }

            Console.WriteLine("Closing...");

            if (server != null)
            {
                server.Stop();
                Console.WriteLine("Stopped server.");
            }
            else if (client != null)
            {
                client.Disconnect();
                Console.WriteLine("Disconnected client.");
            }

            Console.WriteLine("Fin.");
        }
    }
}


