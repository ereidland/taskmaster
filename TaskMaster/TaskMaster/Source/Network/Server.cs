//
// Server.cs
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

namespace TaskMaster.Network
{
    public enum ServerConnectionState
    {
        Disconnected,
        Listening,
    }

    public class ClientConnectedEvent
    {
        public Server Owner { get; private set; }
        public Guid ClientID { get; private set; }

        public ClientConnectedEvent(Server owner, Guid clientID)
        {
            Owner = owner;
            ClientID = clientID;
        }
    }

    public class ClientDisconnectedEvent
    {
        public Server Owner { get; private set; }
        public Guid ClientID { get; private set; }

        public ClientDisconnectedEvent(Server owner, Guid clientID)
        {
            Owner = owner;
            ClientID = clientID;
        }
    }

    public class KickClientRequest
    {
        public Guid ClientID { get; private set; }
        public string Reason { get; private set; }
        public KickClientRequest(Guid client, string reason)
        {
            ClientID = client;
            Reason = reason;
        }
    }

    public abstract class Server : INetworkInterface
    {
        public abstract class ServerClient
        {
            public Server Owner { get; private set; }
            public Guid ID { get; protected set; }
            public abstract NetworkError Send(string text, byte[] binary);
            public abstract void Stop();

            public ServerClient(Server owner, Guid id)
            {
                Owner = owner;
                ID = id;
            }

            public ServerClient(Server owner) : this(owner, Guid.NewGuid()) {}
        }

        private object _stateLock = new object();

        private ActionQueue _actions = new ActionQueue(null);
        protected ActionQueue Actions { get { return _actions; } }

        public EventHub Events { get; private set; }

        private ServerConnectionState _connectionState = ServerConnectionState.Disconnected; 

        public ServerConnectionState State
        {
            get { return _connectionState; }
            protected set
            {
                lock (_stateLock)
                {
                    if (_connectionState != value)
                    {
                        var eventObject = new ObjectChangedEvent<ServerConnectionState>(_connectionState, value);
                        _connectionState = value;
                        Events.SendQueued(Actions, eventObject);
                    }
                }
            }
        }

        public int Port { get; protected set; }

        private Dictionary<Guid, ServerClient> _clients = new Dictionary<Guid, ServerClient>();

        public void ResolveEvents() { _actions.ResolveActions(); }

        protected void AddClient(ServerClient client)
        {
            _clients[client.ID] = client;
        }

        protected void RemoveClient(Guid id)
        {
            if (_clients.ContainsKey(id))
            {
                _clients.Remove(id);
                Events.SendQueued(Actions, new ClientDisconnectedEvent(this, id));
            }
        }

        protected ServerClient GetClient(Guid id)
        {
            ServerClient client;
            _clients.TryGetValue(id, out client);
            return client;
        }

        protected T GetClient<T>(Guid id) where T : ServerClient { return GetClient(id) as T; }

        public List<ServerClient> GetClients() { return GetClients<ServerClient>(); }

        public List<T> GetClients<T>() where T : ServerClient
        {
            List<T> clients = new List<T>();
            foreach (var clientPair in _clients)
            {
                var client = clientPair.Value as T;
                if (client != null)
                    clients.Add(client);
            }

            return clients;
        }

        public abstract NetworkError Listen(int port);
        public abstract NetworkError Send(Guid client, string text, byte[] binary);

        public abstract void Kick(Guid client, string reason);

        protected virtual void OnKickClientRequest(KickClientRequest r) { Kick(r.ClientID, r.Reason); }

        public abstract void Stop();

        public Server(EventHub hub)
        {
            Events = hub;

            Events.AddReceiver<KickClientRequest>(OnKickClientRequest);
        }
    }
}

