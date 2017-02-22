﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SecStream;

namespace MessageFramework
{
    class SslMessageClient : TcpMessageChannel
    {
        StreamSSL _StreamSSL;
        public SslMessageClient(int id, ChannelSettings Settings)
            : base(id, Settings)
        {
            _ChannelSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _StreamSSL = new StreamSSL();
            _StreamSSL.EncryptDataOutput = OnSslEncryptedData;
            _StreamSSL.DecryptDataOutput = OnSslDecryptedData;

        }

        public bool Connect(string host, int port)
        {
            var result = _ChannelSocket.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(_Settings.ConnectTimeout));
            _ChannelSocket.EndConnect(result);
            if (!success)
            {
                Close();
                return false;
            }
            _StreamSSL.Initialize("localhost");
            StartReceive();
            return success;
        }

        protected override void ProcessReceive(SocketAsyncEventArgs ReceiveEventArgs)
        {
            ActiveDateTime = DateTime.Now;
            if (ReceiveEventArgs.SocketError != SocketError.Success ||
                ReceiveEventArgs.BytesTransferred == 0)
            {
                Log.Error(ReceiveEventArgs.SocketError.ToString());
                Close();
                return;
            }
            Log.Debug("Receive " + ReceiveEventArgs.BytesTransferred + " bytes");

            _StreamSSL.Decrypt(_RecvBuffer, 0, ReceiveEventArgs.BytesTransferred);
            StartReceive();
        }

        protected override bool Send(Byte[] buffer, Int32 offset, Int32 count)
        {
            return _StreamSSL.Encrypt(buffer, offset, count);
        }

        public override bool SendMessage<T>(T msg)
        {
            byte[] buffer = ProtobufSerializer.Serialize<T>(msg);
            return _StreamSSL.Encrypt(buffer, 0, buffer.Length);
        }

        bool OnSslEncryptedData(byte[] buffer, int offset, int count)
        {
            return base.Send(buffer, offset, count);
        }

        bool OnSslDecryptedData(byte[] buffer, int offset, int count)
        {
            _RecvStream.Write(buffer, 0, count);
            ProcessReceivedData();
            return true;
        }
    }
}
