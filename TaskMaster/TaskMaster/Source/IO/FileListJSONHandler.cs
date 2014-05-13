//
// FileListJSONHandler.cs
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
using TaskMaster.Network;
using TaskMaster.SimpleJSON;

namespace TaskMaster.IO
{
    public class FileListSyncedEvent
    {
        public FileList List { get; private set; }
        public FileListJSONHandler Handler { get; private set; }
        public FileListSyncedEvent(FileList list, FileListJSONHandler handler)
        {
            List = list; Handler = handler;
        }
    }

    public class FileListJSONHandler
    {
        private FileList _localList = new FileList();
        private FileList _remoteList = null;
        private EventHubSubscriber _subscriber = new EventHubSubscriber();

        private JSONProtocol _protocol;

        public INetworkInterface Net { get; private set; }

        private List<string> _missingFiles = new List<string>();

        public bool IsInitialized { get; private set; }
        private bool _isHost;

        public bool IsHost
        {
            get { return IsInitialized && _isHost; }
            private set { _isHost = value; }
        }

        public bool IsClient
        {
            get { return IsInitialized && !_isHost; }
            private set { _isHost = !value; }
        }

        public EventHub Events { get; private set; }

        private void OnRequestFileList(JSONPacket packet)
        {
            Net.Send(packet.Sender, _protocol.CreateString(_localList.SaveToJSON(), "response_file_list"), null);
        }

        private void OnRequestMissingFile(JSONPacket packet)
        {
            //TODO: helper for reading information from a packet, and maybe less strict policy on kicking clients.
            if (packet.Node != null)
            {
                var fileNode = packet.Node["file"];
                if (fileNode != null)
                {
                    string file = fileNode.Value;
                    if (!string.IsNullOrEmpty(file))
                    {
                        string hash = _localList.GetHash(file);
                        if (!string.IsNullOrEmpty(hash))
                        {
                            JSONClass response = new JSONClass();
                            response["file"] = file;
                            response["hash"] = hash;
                            byte[] binary = _localList.ReadFile(file);
                            if (binary != null)
                            {
                                Net.Send(packet.Sender, _protocol.CreateString(response, "response_file_from_list"), binary);
                            }
                            else
                            {
                                Log.Default.Error("Failed to read file {0} for client {1}! Kicking client...", file, packet.Sender);
                                Events.Send(new KickClientRequest(packet.Sender, "Error when reading file for response."));
                            }
                        }
                        else
                        {
                            Log.Default.Error("Received request from {0} for file {1} that we don't have! Kicking client.", packet.Sender);
                            Events.Send(new KickClientRequest(packet.Sender, "Invalid file request (file not on list)."));
                        }
                    }
                    else
                    {
                        Log.Default.Error("Request from {0} has empty file field.", packet.Sender);
                        Events.Send(new KickClientRequest(packet.Sender, "Invalid file request (empty field)."));
                    }
                }
                else
                {
                    Log.Default.Error("Request from {0} is missing file field.", packet.Sender);
                    Events.Send(new KickClientRequest(packet.Sender, "Invalid file request (missing field)."));
                }
            }
        }

        private void OnRespondToFileList(JSONPacket packet)
        {
            if (_remoteList != null)
                _remoteList.Clear();
            else
                _remoteList = new FileList();

            _remoteList.LoadFromJSON(packet.Node);

            _missingFiles = _localList.GetRequiredFiles(_remoteList);
            if (_missingFiles.Count > 0)
            {
                foreach (var file in _missingFiles)
                {
                    var request = new JSONClass();
                    request["file"] = file;
                    Net.Send(packet.Sender, _protocol.CreateString(request, "request_file_from_list"), null);
                }
            }
            else
                Events.Send(new FileListSyncedEvent(_localList, this));
        }

        private void OnRespondToFile(JSONPacket packet)
        {
            if (packet.Node != null && packet.Binary != null)
            {
                var fileNode = packet.Node["file"];
                var hashNode = packet.Node["hash"];
                if (fileNode != null && hashNode != null)
                {
                    string file = fileNode.Value;
                    string hash = hashNode.Value;

                    if (!string.IsNullOrEmpty(file) && !string.IsNullOrEmpty(hash))
                    {
                        _missingFiles.Remove(file);
                        _localList.WriteFile(file, hash, packet.Binary);

                        if (_missingFiles.Count == 0 && _remoteList != null)
                            Events.Send(new FileListSyncedEvent(_localList, this));
                    }
                }
            }
            //TODO: Error logging.
        }

        public void SendEntireList(Guid target)
        {
            foreach (var file in _localList.GetRequiredFiles(null))
            {
                string hash = _localList.GetHash(file);
                byte[] binary = _localList.ReadFile(file);

                JSONClass response = new JSONClass();
                response["file"] = file;
                response["hash"] = hash;

                Net.Send(target, _protocol.CreateString(response, "response_file_from_list"), binary);
            }
        }

        public void SetupAsHost(JSONProtocol protocol, string dataFolder)
        {
            if (!IsInitialized)
            {
                IsInitialized = true;

                FileList.CreatePathForFile(dataFolder);

                _protocol = protocol;
                Net = _protocol.Net;
                Events = protocol.Events;
                IsHost = true;
                _localList.CalculateFromDirectory(dataFolder, "*");

                _protocol.AddHandler("request_file_list", OnRequestFileList);
                _protocol.AddHandler("request_file_from_list", OnRequestMissingFile);
            }
            else
                Log.Default.Error("Can't call SetupAsHost: Already initialized!");
        }

        public void SetupAsClient(JSONProtocol protocol, string dataFolder, bool requestList = true)
        {
            if (!IsInitialized)
            {
                IsInitialized = true;

                FileList.CreatePathForFile(dataFolder);
                
                _protocol = protocol;
                Net = _protocol.Net;
                Events = _protocol.Events;
                IsHost = false;
                _localList.CalculateFromDirectory(dataFolder, "*");

                _protocol.AddHandler("response_file_list", OnRespondToFileList);
                _protocol.AddHandler("response_file_from_list", OnRespondToFile);

                if (requestList)
                    Net.Send(Guid.Empty, _protocol.CreateString(new JSONClass(), "request_file_list"), null);
            }
            else
                Log.Default.Error("Can't call SetupAsClient: Already initialized!");
        }

        public void Stop()
        {
            _localList.Clear();
            if (_remoteList != null)
                _remoteList.Clear();

            IsInitialized = false;
            _isHost = false;

            _subscriber.UnsubscribeAll();
        }
    }
}

