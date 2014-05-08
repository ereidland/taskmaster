//
// JSONProtocol.cs
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
using TaskMaster.SimpleJSON;

namespace TaskMaster.Network
{
    public class JSONPacket
    {
        public Guid Sender { get; private set; }
        public JSONNode Node { get; private set; }
        public bool FailedToParse { get; private set; }
        public string Text { get; private set; }
        public byte[] Binary { get; private set; }
        public JSONPacket(Packet p)
        {
            Sender = p.Sender;
            Text = p.Text;
            Binary = p.Binary;

            if (Text != null)
            {
                try
                {
                    Node = JSON.Parse(Text);
                }
                catch (Exception e) { Log.Default.Exception(e); }
            }

            FailedToParse = Node == null;
        }
    }

    public class JSONProtocol
    {
        public Server NetServer { get; private set; }
        public Client NetClient { get; private set; }

        public INetworkInterface Net { get { return NetServer != null ? (INetworkInterface)NetServer : (INetworkInterface)NetClient; } }
        public EventHub Events { get; private set; }

        /// <summary>
        /// Always returns true because authentication hasn't been implemented yet.
        /// </summary>
        public bool IsClientAuthenticated(Guid guid)
        {
            //TODO: Actually code authentication.
            return true;
        }

        public static JSONNode SetPacketType(JSONNode source, string type)
        {
            if (source == null)
                return new JSONNode();

            if (!string.IsNullOrEmpty(type))
                source["_packet_type"] = type;

            return source;
        }

        public string CreateString(JSONNode source, string type = null)
        {
            if (source != null)
            {
                if (!string.IsNullOrEmpty(type))
                    source = SetPacketType(source, string.Format("{0}{1}", Prefix, type));
                return source.ToString();
            }
            return null;
        }
            
        public delegate void PacketHandler(JSONPacket packet);
        private Dictionary<string, PacketHandler> _handlers = new Dictionary<string, PacketHandler>(StringComparer.InvariantCultureIgnoreCase);

        public string Prefix { get; private set; }

        public void AddHandler(string type, PacketHandler handler)
        {
            if (handler != null && !string.IsNullOrEmpty(type))
            {
                Log.Default.Debug("Adding handler of type {0}{1}", Prefix, type);
                _handlers[Prefix + type] = handler;
            }
        }

        public void RemoveHandler(string type) { _handlers.Remove(type); }

        private void OnPacket(Packet p)
        {
            if (IsClientAuthenticated(p.Sender))
            {
                JSONPacket jsonPacket = new JSONPacket(p);
                if (jsonPacket.FailedToParse)
                {
                    Log.Default.Error("Failed for parse JSON from sender {0}!", p.Sender);
                }
                else
                {
                    var type = jsonPacket.Node["_packet_type"];
                    if (type != null)
                    {
                        if (string.IsNullOrEmpty(Prefix) || type.Value.StartsWith(Prefix))
                        {
                            PacketHandler handler;
                            if (_handlers.TryGetValue(type.Value, out handler))
                            {
                                try
                                {
                                    handler(jsonPacket);
                                } catch (Exception e) { Log.Default.Exception(e); }
                            }
                            else
                                Log.Default.Error("No handler for packet type, \"{0}\"!", type.Value);
                        }
                        else
                            Log.Default.Debug("Received packet that doesn't start with prefix ({0}): {1}", Prefix, type.Value);
                    }
                    else
                        Log.Default.Error("type is missing from packet from {0}!", p.Sender);
                }
            }
            else
                Log.Default.Error("Client {0} is not authenticated!", p.Sender);
        }

        public JSONProtocol(string prefix, Client client)
        {
            if (client == null)
                throw new ArgumentNullException("client", "Attempted to initialize JSONProtocol without client!");

            if (prefix == null)
                prefix = "";

            Prefix = prefix;

            NetClient = client;
            Events = NetClient.Events;

            Events.AddReceiver<Packet>(OnPacket);
        }

        public JSONProtocol(string prefix, Server server)
        {
            if (server == null)
                throw new ArgumentNullException("server", "Attempted to initialize JSONProtocol without sever!");

            if (prefix == null)
                prefix = "";

            Prefix = prefix;

            NetServer = server;
            Events = NetServer.Events;

            Events.AddReceiver<Packet>(OnPacket);
        }
    }
}

