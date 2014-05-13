//
// InstanceManager.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TaskMaster.Plugin
{
    public class InstanceManager<T> where T : class
    {
        public static IEnumerable<Type> GetAllTypesInAssembly(Assembly assembly, Type baseType)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsClass && !type.IsGenericType && !type.IsAbstract && baseType.IsAssignableFrom(type))
                    yield return type;
            }
        }

        public static IEnumerable<Type> GetAllTypesInAssembly(Assembly assembly, string baseType)
        {
            return GetAllTypesInAssembly(assembly, assembly.GetType(baseType));
        }

        private Dictionary<string, T> _registeredInstances = new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase);

        public void RegisterDefaultInstances(Assembly assembly, params object[] arguments)
        {
            foreach (var type in GetAllTypesInAssembly(assembly, typeof(T)))
            {
                try
                {
                    T instance = Activator.CreateInstance(type, arguments) as T;
                    if (instance != null)
                        _registeredInstances[InstanceNameAttribute.GetName(type)] = instance;
                    else
                        Log.Default.Error("This shouldn't happen: type {0} is not assignable to type {1}, but it passed GetAllTypesInAssembly.", type.FullName, typeof(T).FullName);
                }
                catch (Exception e) { Log.Default.Exception(e); }
            }
        }

        public void RegisterDefaultInstances(params object[] arguments) { RegisterDefaultInstances(Assembly.GetExecutingAssembly(), arguments); }

        public void UnregisterTypesInAssembly(Assembly assembly)
        {
            List<string> toRemove = new List<string>();
            foreach (var pair in _registeredInstances)
            {
                if (pair.Value.GetType().Assembly == assembly)
                    toRemove.Add(pair.Key);
            }

            foreach (var key in toRemove)
                _registeredInstances.Remove(key);
        }

        public T GetInstance(string registeredName)
        {
            if (!string.IsNullOrEmpty(registeredName))
            {
                T instance;
                _registeredInstances.TryGetValue(registeredName, out instance);
                return instance;
            }

            return null;
        }
    }
}

