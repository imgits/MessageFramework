﻿using System;
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
    class TcpMessageChannel
    {
        public Socket ChannelSocket { get; set; }
        public SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        public SocketAsyncEventArgs SendEventArgs { get; set; }
        public byte[] ReceiveBuffer;
        public int ReceiveBufferOffet;
        public int ReceiveBufferSize;
        public DateTime ActiveDateTime;
        public int ChannelId { get; set;}
        public bool UseSSL { get; set; }
        public X509Certificate Certificate { get; set; }

        bool    _IsServerChannel;
        int     _ReceivedBytes;
        int     _SendLocked = 0;
        ConcurrentQueue<object> _SendMessageQueue;

        public TcpMessageChannel()
        {
            ChannelSocket = null;
            ReceiveEventArgs = new SocketAsyncEventArgs();
            SendEventArgs = new SocketAsyncEventArgs();
            ReceiveEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveEvent_Completed);
            SendEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(SendEvent_Completed);
            ActiveDateTime = DateTime.Now;

            _SendMessageQueue = new ConcurrentQueue<object>();
            _IsServerChannel = true;
            _ReceivedBytes = 0;
            _SendLocked = 0;
            UseSSL = false;
        }

        /// <summary>
        /// 客户端验证服务器证书
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;

            // Do not allow this client to communicate with unauthenticated servers.
            return true;
        }

        public bool Connect(string host, int port, int timeout)
        {
            _IsServerChannel = false;
            var result = ChannelSocket.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeout));
            if (!success)
            {
                ChannelSocket.Close();
                ChannelSocket.EndConnect(result);
                return false;
            }

            return success;
        }
        public void Accept()
        {
            if (UseSSL)
            {

            }
        }

        public bool StartReceive()
        {
            if (ChannelSocket.ReceiveAsync(ReceiveEventArgs))
            {
                ProcessReceive(ReceiveEventArgs);
            }
            return true;
        }

        void ReceiveEvent_Completed(object sender, SocketAsyncEventArgs ReceiveEventArgs)
        {
            ProcessReceive(ReceiveEventArgs);
        }

        private void ProcessReceive(SocketAsyncEventArgs ReceiveEventArgs)
        {
            ActiveDateTime = DateTime.Now;
            if (ReceiveEventArgs.SocketError != SocketError.Success ||
                ReceiveEventArgs.BytesTransferred == 0)
            {
                Close();
                return;
            }
            _ReceivedBytes += ReceiveEventArgs.BytesTransferred;
            int offset = ReceiveBufferOffet;

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
                Buffer.BlockCopy(ReceiveBuffer, offset, ReceiveBuffer, ReceiveBufferOffet, _ReceivedBytes);
            }

        }

        void ProcessReceivedPacket(byte[] buffer, int offset, int count)
        {
            //消息解码必须同步完成，否则buffer中的数据可能会出现一致性问题
            //Message msg = Message.Decode(buffer, offset, count);
            //if (msg == null) throw new ObjectDisposedException("Bad packet");
            //OnReceivedMessage(msg);
        }

        public bool SendMessage<T>(T msg) where T: class
        {
            if (Interlocked.CompareExchange(ref _SendLocked,1,0)==1)
            {
                _SendMessageQueue.Enqueue(msg);
                //将包放进队列
            }
            else
            {
                //SendEventArgs.SetBuffer(buffer, offset, count);
                if (ChannelSocket.SendAsync(SendEventArgs))
                {
                    //return ProcessSend(SendEventArgs);
                }
                else
                    return true;
            }
            return true;
        }

        void SendEvent_Completed(object sender, SocketAsyncEventArgs arg)
        {
            Interlocked.Exchange(ref _SendLocked, 0);
            object msg = null;
            if (_SendMessageQueue.TryDequeue(out msg))
            {
                if (Interlocked.CompareExchange(ref _SendLocked, 1, 0) != 1)
                {
                    //SendEventArgs.SetBuffer(buffer, offset, count);
                    if (ChannelSocket.SendAsync(SendEventArgs))
                    {
                        //return ProcessSend(SendEventArgs);
                    }
                }
            }
        }

        public void Close()
        {
            if (ChannelSocket != null) ChannelSocket.Shutdown(SocketShutdown.Both);
            ChannelSocket = null;
        }
    }
}
