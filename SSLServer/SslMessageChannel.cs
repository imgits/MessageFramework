
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using StreamSSL;

namespace MessageFramework
{
    class SslMessageChannel : TcpMessageChannel
    {
        readonly X509Certificate2 _Certificate;
        private SslServerStream _SslServerStream;

        public SslMessageChannel(int id, ChannelSettings Settings, X509Certificate2 Certificate)
            :base(id, Settings)
        {
            _Certificate = Certificate;
            _SslServerStream = new SslServerStream();
            _SslServerStream.ServerTokenOutput = OnSslEncryptedData;
            _SslServerStream.EncryptDataOutput = OnSslEncryptedData;
            _SslServerStream.DecryptDataOutput = OnSslDecryptedData;
        }

        public override void Accept(Socket ClientSocket)
        {
            _IsServerChannel = true;
            _ChannelSocket = ClientSocket;
            ReceiveEventArgs.AcceptSocket = _ChannelSocket;
            
            _SslServerStream.CreateCredentials(_Certificate);
            
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

            if (!_SslServerStream.IsAuthenticated)
            {
                _SslServerStream.AcceptClientToken(_RecvBuffer, 0, ReceiveEventArgs.BytesTransferred);
            }
            else
            {
                _SslServerStream.Decrypt(_RecvBuffer, 0, ReceiveEventArgs.BytesTransferred);
            }
            StartReceive();
        }

        protected override bool Send(Byte[] buffer, Int32 offset, Int32 count)
        {
            return _SslServerStream.Encrypt(buffer, offset, count);
        }

        public override bool SendMessage<T>(T msg)
        {
            byte[] buffer = ProtobufSerializer.Serialize<T>(msg);
            return _SslServerStream.Encrypt(buffer, 0, buffer.Length);
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
