using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SSLServer
{

    class SslMessageChannel : TcpMessageChannel
    {
        public X509Certificate Certificate { get; set; }
        private byte[] SslRecvBuffer = new byte[4096];

        public SslMessageChannel() : base()
        {
            ReceiveEventArgs.SetBuffer(SslRecvBuffer, 0, SslRecvBuffer.Length);
        }

        /// <summary>
        /// 客户端验证服务器证书
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;

            // Do not allow this client to communicate with unauthenticated servers.
            return true;
        }

        public override bool Connect(string host, int port, int timeout)
        {
            _IsServerChannel = false;
            var result = _ChannelSocket.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeout));
            if (!success)
            {
                _ChannelSocket.Close();
                _ChannelSocket.EndConnect(result);
                return false;
            }

            return success;
        }

        public override void Accept(Socket ClientSocket)
        {
            _IsServerChannel = true;
            _ChannelSocket = ClientSocket;
            ReceiveEventArgs.AcceptSocket = _ChannelSocket;
            StartReceive();
        }

        protected override void ProcessReceive(SocketAsyncEventArgs ReceiveEventArgs)
        {
            ActiveDateTime = DateTime.Now;
            if (ReceiveEventArgs.SocketError != SocketError.Success ||
                ReceiveEventArgs.BytesTransferred == 0)
            {
                Close();
                return;
            }
            _ReceivedBytes += ReceiveEventArgs.BytesTransferred;
            int offset = ReceiveBufferOffset;

            while (_ReceivedBytes >= 2)
            {
                //packet_size包含自身的2字节长度
                int packet_size = BitConverter.ToUInt16(ReceiveBuffer, offset);
                if (_ReceivedBytes < packet_size) break;
                ProcessReceivedPacket(ReceiveBuffer, offset + 2, packet_size - 2);
                _ReceivedBytes -= packet_size;
                offset += packet_size;
            }
            if (_ReceivedBytes > 0)
            {
                Buffer.BlockCopy(ReceiveBuffer, offset, ReceiveBuffer, ReceiveBufferOffset, _ReceivedBytes);
            }

        }

        public override bool SendMessage<T>(T msg)
        {
            if (Interlocked.CompareExchange(ref _SendLocked, 1, 0) == 1)
            {
                _SendMessageQueue.Enqueue(msg);
                //将包放进队列
            }
            else
            {
                //SendEventArgs.SetBuffer(buffer, offset, count);
                if (_ChannelSocket.SendAsync(SendEventArgs))
                {
                    //return ProcessSend(SendEventArgs);
                }
                else
                    return true;
            }
            return true;
        }

    }
}
