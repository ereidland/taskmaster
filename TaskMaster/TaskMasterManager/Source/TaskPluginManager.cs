//
// TaskPluginManager.cs
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
using TaskMaster;
using TaskMaster.SimpleJSON;
using TaskMaster.Plugin;

namespace TaskMaster.Manager
{
    public enum TaskResult
    {
        Failed,
        NoSuchPlugin,
        Success,
    }

    public class TaskPluginManager
    {
        private InstanceManager<ITaskMasterPlugin> _instanceManager = new InstanceManager<ITaskMasterPlugin>();
        private WorkerThreadPool _workers = new WorkerThreadPool();

        private class AsyncTaskExecuter
        {
            public string Plugin { get; private set; }
            public JSONNode TaskInfo { get; private set; }
            public Action<TaskResult, Guid> Callback { get; private set; }
            public Guid ID { get; private set; }
            public TaskPluginManager Manager { get; private set; }

            public void Resolve()
            {
                TaskResult result = Manager.ExecuteTask(Plugin, TaskInfo);
                if (Callback != null)
                    Callback(result, ID);
            }

            public AsyncTaskExecuter(string plugin, JSONNode taskInfo, Action<TaskResult, Guid> callback, Guid id)
            {
                Plugin = plugin;
                TaskInfo = taskInfo;
                Callback = callback;
                ID = id;
            }
        }

        public TaskResult ExecuteTask(string plugin, JSONNode taskInfo)
        {
            var pluginInstance = _instanceManager.GetInstance(plugin);

            try
            {
                if (pluginInstance != null)
                    return pluginInstance.ExecuteTask(taskInfo) ? TaskResult.Success : TaskResult.Failed;
            }
            catch (Exception e)
            {
                Log.Default.Exception(e);
                return TaskResult.Failed;
            }

            return TaskResult.NoSuchPlugin;
        }

        public Guid ExecuteTaskAsync(string plugin, JSONNode taskInfo, Action<TaskResult, Guid> callback)
        {
            Guid taskID = Guid.NewGuid();

            _workers.AddWork(new AsyncTaskExecuter(plugin, taskInfo, callback, taskID).Resolve);

            return taskID;
        }
    }
}

