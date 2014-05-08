//
// WorkingSet.cs
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
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TaskMaster.SimpleJSON;

namespace TaskMaster
{
    public class FileList
    {
        //Attempt to protect against potential requests that would get files on disk out of bounds.
        public static bool IsPathSafe(string path)
        {
            return path != null && !Path.IsPathRooted(path) && !path.Contains("~") && !path.Contains("..");
        }
        public static void CreatePathForFile(string filePath)
        {
            int slashIndex = filePath.LastIndexOf('/');
            if (slashIndex != -1)
                Directory.CreateDirectory(filePath.Substring(0, slashIndex));
        }

        private MD5 _md5 = MD5.Create();
        private Dictionary<string, string> _filesByHash = new Dictionary<string, string>();
        public string BaseDirectory { get; private set; }

        public void Clear()
        {
            lock (_filesByHash)
                _filesByHash.Clear();
        }

        public void CalculateFromDirectory(string path, string pattern = null)
        {
            BaseDirectory = path.Replace('\\', '/');

            if (string.IsNullOrEmpty(pattern))
                pattern = "*";

            string[] files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    string filePath = file.Replace('\\', '/');

                    byte[] fileData = File.ReadAllBytes(filePath);

                    string hash = BitConverter.ToString(_md5.ComputeHash(fileData)).Replace("-", "");
                    int length = BaseDirectory.Length;
                    if (!BaseDirectory.EndsWith("/"))
                        length++;

                    filePath = filePath.Substring(length);

                    lock (_filesByHash)
                        _filesByHash[filePath] = hash;

                    Log.Default.Info("File: {0} Hash: {1}", filePath, hash);
                }
                catch (Exception e) { Log.Default.Exception(e); }
            }
        }

        public bool HasFile(string name) { return _filesByHash.ContainsKey(name); }
        public string GetHash(string name)
        {
            string hash;
            _filesByHash.TryGetValue(name, out hash);
            return hash;
        }

        public byte[] ReadFile(string name)
        {
            if (IsPathSafe(name))
            {
                try
                {
                    return File.ReadAllBytes(Path.Combine(BaseDirectory, name));
                } catch (Exception e) { Log.Default.Exception(e); }
            }
            else
                Log.Default.Error("Attempted to read unsafe file: {0}", name);
            return null;
        }

        public void WriteFile(string name, string hash, byte[] binary)
        {
            string fullPath = Path.Combine(BaseDirectory, name);
            CreatePathForFile(fullPath);
            string actualHash = BitConverter.ToString(_md5.ComputeHash(binary)).Replace("-", "");
            if (actualHash != hash)
                Log.Default.Warning("Hash for file {0} does not match the associated hash. {1} != {2}", name, actualHash, hash);

            _filesByHash[name] = actualHash;
            File.WriteAllBytes(fullPath, binary);
        }

        public List<string> GetRequiredFiles(FileList other)
        {
            List<string> missing = new List<string>();
            if (other != null)
            {
                lock (_filesByHash)
                {
                    foreach (var filePair in other._filesByHash)
                    {
                        string hash;
                        if (!File.Exists(BaseDirectory + filePair.Key) || !_filesByHash.TryGetValue(filePair.Key, out hash) || hash != filePair.Value)
                            missing.Add(filePair.Key);
                    }
                }
            }
            return missing;
        }

        public JSONNode SaveToJSON()
        {
            JSONClass node = new JSONClass();
            JSONClass fileHashes = new JSONClass();

            lock (_filesByHash)
            {
                foreach (var filePair in _filesByHash)
                    fileHashes[filePair.Key] = filePair.Value.ToString();
            }

            node["files"] = fileHashes;

            return node;
        }

        public void LoadFromJSON(JSONNode node)
        {
            if (node != null)
            {
                var files = node["files"];
                if (files != null && files is JSONClass)
                {
                    lock (_filesByHash)
                    {
                        foreach (KeyValuePair<string, JSONNode> pair in files as JSONClass)
                            _filesByHash[pair.Key] = pair.Value;
                    }
                }
                else
                {
                    if (files == null)
                        Log.Default.Error("Can't load files list because the node does not contain it.");
                    else
                        Log.Default.Error("Can't load files because files node is not a JSONClass");
                }
            }
        }

        public void LoadFromFile(string fileName)
        {
            try
            {
                LoadFromFile(JSON.Parse(File.ReadAllText(fileName)));
            } catch (Exception e) { Log.Default.Exception(e); }
        }

        public void SaveToFile(string fileName)
        {
            File.WriteAllText(fileName, SaveToJSON().ToString(), System.Text.Encoding.UTF8);
        }
    }
}

