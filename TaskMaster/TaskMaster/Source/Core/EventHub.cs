//
// EventHub.cs
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

namespace TaskMaster
{
    public abstract class EventCallbackHandler
    {
        public object TargetObject { get; protected set; }
        public abstract void Execute(object argument);
    }

    public class ActionEventCallbackHandler<T> : EventCallbackHandler
    {
        public Action<T> Callback { get; private set; }

        public override void Execute (object argument)
        {
            if (argument is T)
                Callback((T)argument);
        }

        public ActionEventCallbackHandler(Action<T> callback)
        {
            Callback = callback;
            TargetObject = Callback;
        }
    }

    public class VoidActionEventCallbackHandler : EventCallbackHandler
    {
        public Action Callback { get; private set; }

        public override void Execute (object argument) { Callback(); }

        public VoidActionEventCallbackHandler(Action callback)
        {
            Callback = callback;
            TargetObject = Callback;
        }
    }

    public class EventHubSubscription
    {
        public EventHub Hub { get; private set; }

        public object EventKey { get; private set; }
        public object TargetObject { get; private set; }

        public bool Canceled { get; private set; }

        public void Cancel() { Hub.RemoveReceiver(EventKey, TargetObject); }

        public EventHubSubscription(EventHub hub, object key, object targetObject)
        {
            Hub = hub;
            EventKey = key;
            TargetObject = TargetObject;
        }
    }

    public class EventHubSubscriber
    {
        private List<EventHubSubscription> _subscriptions = new List<EventHubSubscription>();

        private void AddSubscription(EventHubSubscription subscription)
        {
            if (subscription != null)
            {
                lock (_subscriptions)
                    _subscriptions.Add(subscription);
            }
        }

        public void Unsubscribe(EventHub hub)
        {
            lock (_subscriptions)
            {
                _subscriptions.RemoveAll(subscription =>
                {
                    if (subscription.Hub == hub)
                    {
                        subscription.Cancel();
                        return true;
                    }
                    return false;
                });
            }
        }

        public void UnsubscribeAll()
        {
            lock (_subscriptions)
            {
                foreach (var subscription in _subscriptions)
                    subscription.Cancel();

                _subscriptions.Clear();
            }
        }

        public void AddReceiver(EventHub hub, object key, EventCallbackHandler callback) { AddSubscription(hub.AddReceiver(key, callback)); }
        public void AddReceiver<T>(EventHub hub, Action<T> callback) { AddSubscription(hub.AddReceiver<T>(callback)); }
        public void AddReceiver(EventHub hub, string eventName, Action callback) { AddSubscription(hub.AddReceiver(eventName, callback)); }
    }

    public class EventHub
    {
        private Dictionary<object, List<EventCallbackHandler>> _handlers = new Dictionary<object, List<EventCallbackHandler>>();

        public EventHubSubscription AddReceiver(object key, EventCallbackHandler callback)
        {
            if (callback != null && callback.TargetObject != null)
            {
                lock (_handlers)
                {
                    List<EventCallbackHandler> handlerList;
                    if (_handlers.TryGetValue(key, out handlerList))
                    {
                        foreach (var handler in handlerList)
                        {
                            if (callback.TargetObject == handler.TargetObject)
                                return null;
                        }
                    }
                    else
                    {
                        handlerList = new List<EventCallbackHandler>();
                        _handlers[key] = handlerList;
                    }

                    handlerList.Add(callback);
                    return new EventHubSubscription(this, key, callback.TargetObject);
                }
            }
            return null;
        }

        public void RemoveReceiver(object key, object targetObject)
        {
            if (key != null)
            {
                lock (_handlers)
                {
                    List<EventCallbackHandler> handlerList;
                    if (_handlers.TryGetValue(key, out handlerList))
                    {
                        handlerList.RemoveAll((handler) =>
                        {
                            return handler.TargetObject == targetObject;
                        });

                        if (handlerList.Count == 0)
                            _handlers.Remove(key);
                    }
                }
            }
        }

        public EventHubSubscription AddReceiver<T>(Action<T> callback) { return AddReceiver(typeof(T), new ActionEventCallbackHandler<T>(callback)); }
        public void RemoveReceiver<T>(Action<T> callback) { RemoveReceiver(typeof(T), callback); }

        public EventHubSubscription AddReceiver(string eventName, Action callback) { return AddReceiver(eventName, new VoidActionEventCallbackHandler(callback)); }
        public void RemoveReceiver(string eventName, Action callback) { RemoveReceiver(eventName, callback); }

        public bool Send(object key, object eventObject)
        {
            bool anyReceived = false;
            if (key != null)
            {
                List<EventCallbackHandler> handlerList;

                lock (_handlers)
                {
                    if (_handlers.TryGetValue(key, out handlerList))
                    {
                        foreach (var handler in handlerList)
                        {
                            try { handler.Execute(eventObject); }
                            catch (Exception e) { Log.Default.Exception(e); } //TODO: Engine independant exception logging. Maybe send off an exception event?
                        }
                    }
                }
            }
            return anyReceived;
        }

        public bool Send(object eventObject)
        {
            if (eventObject != null)
                return Send(eventObject.GetType(), eventObject);
            return false;
        }

        public bool Send<T>(T eventObject) { return Send(typeof(T), eventObject); }

        public void SendQueued(ActionQueue queue, object key, object eventObject) { queue.AddAction(() => { Send(key, eventObject); }); }

        public void SendQueued(ActionQueue queue, object eventObject)
        {
            if (eventObject != null)
                SendQueued(queue, eventObject.GetType(), eventObject);
        }

        public void SendQueued<T>(ActionQueue queue, T eventObject) { SendQueued(queue, typeof(T), eventObject); }
    }
}
