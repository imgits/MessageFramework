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
        public X509Certificate Certificate { get; set; }

        bool    is_server_channel;
        int     m_received_bytes;
        int     m_send_locked = 0;
        ConcurrentQueue<object> m_send_message_queue;

        public TcpMessageChannel()
        {
            ChannelSocket = null;
            ReceiveEventArgs = new SocketAsyncEventArgs();
            SendEventArgs = new SocketAsyncEventArgs();
            ReceiveEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveEvent_Completed);
            SendEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(SendEvent_Completed);
            ActiveDateTime = DateTime.Now;

            m_send_message_queue = new ConcurrentQueue<object>();
            is_server_channel = true;
            m_received_bytes = 0;
            m_send_locked = 0;
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
            is_server_channel = false;
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
            m_received_bytes += ReceiveEventArgs.BytesTransferred;
            int offset = ReceiveBufferOffet;

            while (m_received_bytes >= 2)
            {
                //packet_size包含自身的2字节长度
                int packet_size = BitConverter.ToUInt16(ReceiveBuffer, offset);
                if (m_received_bytes < packet_size) break;
                ProcessReceivedPacket(ReceiveBuffer, offset + 2, packet_size - 2);
                m_received_bytes -= packet_size;
                offset += packet_size;
            }
            if (m_received_bytes > 0)
            {
                Buffer.BlockCopy(ReceiveBuffer, offset, ReceiveBuffer, ReceiveBufferOffet, m_received_bytes);
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
            if (Interlocked.CompareExchange(ref m_send_locked,1,0)==1)
            {
                m_send_message_queue.Enqueue(msg);
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
            Interlocked.Exchange(ref m_send_locked, 0);
            object msg = null;
            if (m_send_message_queue.TryDequeue(out msg))
            {
                if (Interlocked.CompareExchange(ref m_send_locked, 1, 0) != 1)
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
