//
// WorkerThreadPool.cs
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
using System.Threading;

namespace TaskMaster
{
    public class WorkerThreadPool
    {
        private class Worker
        {
            public WorkerThreadPool Owner { get; private set; }
            public bool StopsOnWorkComplete { get; private set; }
            private Thread _activeThread;
            private Action _targetAction;

            private void Work()
            {
                while (_activeThread != null)
                {
                    try
                    {
                        if (_targetAction != null)
                        {
                            var work = _targetAction;
                            _targetAction = null;
                            work();
                        }
                        else
                            Owner._pendingWork.ResolveSingleAction();

                        Thread.Sleep(Owner.WorkerSleepInterval);
                    } catch (Exception e) { Log.Default.Exception(e); }

                    if (StopsOnWorkComplete)
                        Stop();
                }
            }

            public void Start()
            {
                if (_activeThread == null)
                {
                    _activeThread = new Thread(t => Work());
                    _activeThread.Start();
                }
            }

            
            public void Stop() { _activeThread = null; }

            public Worker(WorkerThreadPool owner, bool stopOnComplete = false, Action targetAction = null)
            {
                Owner = owner;
                StopsOnWorkComplete = stopOnComplete;
                _targetAction = targetAction;
            }
        }

        private List<Worker> _workers = new List<Worker>();
        private ActionQueue _pendingWork = new ActionQueue();

        public void AddWork(Action action) { _pendingWork.AddAction(action); }

        public int TotalWorkers
        {
            get { return _workers.Count; }
        }

        private int _workerSleepInterval = 100;
        public int WorkerSleepInterval
        {
            get { return _workerSleepInterval; }
            set { _workerSleepInterval = Math.Max(_workerSleepInterval, 0); }
        }

        public void DoWork(Action action, bool createWorker = false)
        {
            if (createWorker)
                AddWorker(action);
            else
            {
                if (TotalWorkers == 0)
                    AddWorker();

                _pendingWork.AddAction(action);
            }
        }

        public void AddWorker(Action targetAction = null)
        {
            Worker worker = new Worker(this, targetAction != null, targetAction);
            lock (_workers)
                _workers.Add(worker);

            worker.Start();
        }

        public void Stop()
        {
            foreach (var worker in _workers)
                worker.Stop();
        }

        public WorkerThreadPool(int sleepInterval = 100)
        {
            WorkerSleepInterval = sleepInterval;
        }
    }
}