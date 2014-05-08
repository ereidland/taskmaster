//
// Shared.cs
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
using System.IO;
using System.Net;
using System.Text;

namespace TaskMaster.Network
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
    }

    public enum NetworkError
    {
        None,
        NotConnected,
        AlreadyConnecting,
        AlreadyConnected,
        AlreadyListening,
        FailedToConnect,
        FailedToListen,
        LostConnection,
        NoSuchClient,
    }

    public class ClientEvent
    {
        public Client Client { get; private set; }
    }

    public interface INetworkInterface
    {
        NetworkError Send(Guid sender, string text, byte[] binary);
    }

    public class Packet
    {
        private static UTF8Encoding _encoding = new UTF8Encoding();
        public Guid Sender { get; private set; }
        public string Text { get; private set; }
        public byte[] Binary { get; private set; }

        public void WriteToStream(BinaryWriter writer)
        {
            byte[] textAsBytes = !string.IsNullOrEmpty(Text) ? _encoding.GetBytes(Text) : null;

            int stringLength = textAsBytes != null ? textAsBytes.Length : 0;
            int binaryLength = Binary != null ? Binary.Length : 0;

            writer.Write(stringLength);
            writer.Write(binaryLength);

            if (stringLength > 0)
                writer.Write(textAsBytes);

            if (binaryLength > 0)
                writer.Write(Binary);
        }

        public static Packet ReadFromStream(Guid sender, BinaryReader reader)
        {
            int stringLength = reader.ReadInt32();
            int binaryLength = reader.ReadInt32();

            string text = null;
            byte[] binary = null;
            if (stringLength > 0)
            {
                byte[] textAsBytes = reader.ReadBytes(stringLength);
                text = _encoding.GetString(textAsBytes);
            }

            if (binaryLength > 0)
                binary = reader.ReadBytes(binaryLength);

            return new Packet(sender, text, binary);
        }

        public Packet(Guid sender, string text, byte[] binary)
        {
            Sender = sender;
            Text = text;
            Binary = binary;
        }
    }
}