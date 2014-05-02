//
// Client.cs
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

namespace TaskMaster.Network
{
    public class Client
    {
        private ActionQueue _actions = new ActionQueue();
        protected ActionQueue Actions { get { return _actions; } }

        public EventHub Events { get; private set; }

        private ConnectionState _connectionState = ConnectionState.Disconnected;

        public ConnectionState State
        {
            get { return _connectionState; }
            set
            {
                if (_connectionState != value)
                {
                    var eventObject = new ObjectChangedEvent<ConnectionState>(_connectionState, value);
                    _connectionState = value;
                    Events.SendQueued(Actions, eventObject);
                }
            }
        }

        public bool IsConnected { get { return State == ConnectionState.Connected; } }

        public string Address { get; protected set; }
        public short Port { get; protected set; }

        public Client(EventHub hub)
        {
            Events = hub;
        }
    }
}

