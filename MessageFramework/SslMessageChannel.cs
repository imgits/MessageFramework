﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SecStream;

namespace MessageFramework
{
    public class SslMessageChannel : TcpMessageChannel
    {
        readonly X509Certificate2 _Certificate;
        private StreamSSL _StreamSSL;

        public SslMessageChannel(int id, ChannelSettings Settings, X509Certificate2 Certificate)
            :base(id, Settings)
        {
            _Certificate = Certificate;
            _StreamSSL = new StreamSSL();
            _StreamSSL.EncryptOutput = OnSslEncryptedData;
            _StreamSSL.DecryptOutput = OnSslDecryptedData;
        }

        public override void Start(Socket ClientSocket)
        {
            _ChannelSocket = ClientSocket;
            ReceiveEventArgs.AcceptSocket = _ChannelSocket;
            
            _StreamSSL.Initialize(_Certificate);
            
            StartReceive();
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
