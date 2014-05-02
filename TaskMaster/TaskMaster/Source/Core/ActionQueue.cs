//
// ActionQueue.cs
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
using System.Collections.Generic;

namespace TaskMaster
{
    /// <summary>
    /// Thread-safe action management.
    /// Use is intended for threads to add actions to the ActionQueue,
    /// and have the main thread call ResolveActions repeatedly on an interval.
    /// </summary>
    public class ActionQueue
    {
        public interface IPendingAction
        {
            void Resolve();
        }

        public class PendingAction<T> : IPendingAction
        {
            public Action<T> Callback { get; private set; }
            public T ActionData { get; private set; }

            public void Resolve()
            {
                if (Callback != null)
                    Callback(ActionData);
            }

            public PendingAction(Action<T> callback, T actionData)
            {
                Callback = callback;
                ActionData = actionData;
            }
        }

        private List<Action> _pendingActions = new List<Action>();
        private Thread _targetThread;

        public void AddAction(Action action)
        {
            if (action != null)
                lock (_pendingActions) { _pendingActions.Add(action); }
        }
        public void AddAction(IPendingAction action)
        {
            if (action != null)
                AddAction(action.Resolve);
        }

        public void ResolveActions()
        {
            if (_targetThread == null || Thread.CurrentThread == _targetThread)
            {
                lock(_pendingActions)
                {
                    try
                    {
                        foreach (Action pendingAction in _pendingActions)
                        {
                            if (pendingAction != null)
                                pendingAction();
                        }
                    }
                    catch (Exception e)
                    {
                        //TODO: Make a default/global event system that sends out a notification that an exception occurred.
                        Log.Default.Exception(e);
                    }

                    _pendingActions.Clear();
                }
            }
            else
                Log.Default.Warning("Attempting to call ResolveActions on thread \"{0}\" and not the target thread, \"{1}\"", Thread.CurrentThread.Name, _targetThread.Name);
        }

        public ActionQueue() : this(Thread.CurrentThread) {}

        public ActionQueue(Thread targetThread)
        {
            _targetThread = targetThread;
        }
    }
}