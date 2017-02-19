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
        public SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        public SocketAsyncEventArgs SendEventArgs { get; set; }
        public byte[] ReceiveBuffer;
        public int ReceiveBufferOffset;
        public int ReceiveBufferSize;
        public DateTime ActiveDateTime;
        public int ChannelId { get; set;}

        protected Socket _ChannelSocket { get; set; }
        protected bool      _IsServerChannel;
        protected int       _ReceivedBytes;
        protected int       _SendLocked = 0;
        protected ConcurrentQueue<object> _SendMessageQueue;

        public bool UseSSL = false;
        public X509Certificate2 Certificate { get; set; }
        private byte[] _SslRecvBuffer = new byte[4096];
        private SslStream _SslStream;
        private SocketStream _SocketStream;
        private ByteStream _SendStream;
        private byte[] _SendBuffer = new byte[4096];
        public TcpMessageChannel()
        {
            _ChannelSocket = null;
            ReceiveEventArgs = new SocketAsyncEventArgs();
            SendEventArgs = new SocketAsyncEventArgs();
            ReceiveEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveEvent_Completed);
            SendEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(SendEvent_Completed);
            ActiveDateTime = DateTime.Now;

            _SendStream = new ByteStream();
            _SendMessageQueue = new ConcurrentQueue<object>();
            _IsServerChannel = true;
            _ReceivedBytes = 0;
            _SendLocked = 0;
        }

        public virtual bool Connect(string host, int port, int timeout)
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
              X509Certificate2 certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;

            // Do not allow this client to communicate with unauthenticated servers.
            return true;
        }

        private void EndAuthenticateAsServer(IAsyncResult result)
        {
            try
            {
                _SslStream.EndAuthenticateAsServer(result); 
                if (_SslStream.IsAuthenticated)
                {
                    _SslStream.ReadTimeout = -1;
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                
            }
        }

        public virtual void Accept(Socket ClientSocket)
        {
            _IsServerChannel = true;
            _ChannelSocket = ClientSocket;
            ReceiveEventArgs.AcceptSocket = _ChannelSocket;
            if (UseSSL)
            {
                _SocketStream = new SocketStream();
                _SocketStream.OnRecvData += OnSslDecryptedData;
                _SocketStream.OnRecvByte += OnSslDecryptedByte;
                _SslStream = new SslStream(_SocketStream);
                _SslStream.BeginAuthenticateAsServer(Certificate, EndAuthenticateAsServer,null);
            }
            StartReceive();
        }

        protected bool StartReceive()
        {
            if (UseSSL) ReceiveEventArgs.SetBuffer(_SslRecvBuffer, 0, _SslRecvBuffer.Length);
            else ReceiveEventArgs.SetBuffer(ReceiveBuffer, ReceiveBufferOffset, ReceiveBufferSize - ReceiveBufferOffset);
            if (!_ChannelSocket.ReceiveAsync(ReceiveEventArgs))
            {
                Log.Debug("ChannelSocket.ReceiveAsync 事件已同步完成");
                ProcessReceive(ReceiveEventArgs);
            }
            return true;
        }

        void ReceiveEvent_Completed(object sender, SocketAsyncEventArgs ReceiveEventArgs)
        {
            Log.Debug("ChannelSocket.ReceiveAsync 异步事件完成");
            ProcessReceive(ReceiveEventArgs);
        }

        protected virtual void ProcessReceive(SocketAsyncEventArgs ReceiveEventArgs)
        {
            ActiveDateTime = DateTime.Now;
            if (ReceiveEventArgs.SocketError != SocketError.Success)
            {
                Log.Error(ReceiveEventArgs.SocketError.ToString());
                Close();
                return;
            }
            Log.Debug("Receive " + ReceiveEventArgs.BytesTransferred + " bytes");
            if (ReceiveEventArgs.BytesTransferred == 0)
            {
                StartReceive();
                return;
            }
            if (UseSSL)
            {
                _SocketStream.WriteRecvData(_SslRecvBuffer, 0, ReceiveEventArgs.BytesTransferred);
                while (_SslStream.IsAuthenticated)
                {
                    try
                    {
                        int Bytes = _SslStream.Read(ReceiveBuffer, ReceiveBufferOffset, ReceiveBufferSize - ReceiveBufferOffset);
                        _ReceivedBytes += Bytes;
                        ProcessReceivedData();
                    }
                    catch(TimeoutException ex)
                    {
                        Log.Warn(ex.Message);
                        break;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            int Bytes = _SslStream.Read(ReceiveBuffer, ReceiveBufferOffset, ReceiveBufferSize - ReceiveBufferOffset);
                        }
                        catch (Exception ex1)
                        {
                        }
                            Log.Warn(ex.Message);
                        break;
                    }
                }
            }
            else
            {
                _ReceivedBytes += ReceiveEventArgs.BytesTransferred;
                ProcessReceivedData();
            }
            StartReceive();
        }

        protected void ProcessReceivedData()
        {
            int offset = ReceiveBufferOffset;
            string msg = Encoding.ASCII.GetString(ReceiveBuffer, ReceiveBufferOffset, _ReceivedBytes);
            Console.Write(msg);
            Send(ReceiveBuffer, ReceiveBufferOffset, _ReceivedBytes);
            ReceiveBufferOffset = 0;
            _ReceivedBytes = 0;
            return;
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
                ReceiveBufferOffset = 0;
            }
        }

        protected void ProcessReceivedPacket(byte[] buffer, int offset, int count)
        {
            //消息解码必须同步完成，否则buffer中的数据可能会出现一致性问题
            //Message msg = Message.Decode(buffer, offset, count);
            //if (msg == null) throw new ObjectDisposedException("Bad packet");
            //OnReceivedMessage(msg);
        }

        void OnSslDecryptedData(Byte[] buffer, Int32 offset, Int32 count)
        {
            Log.Debug(count + " bytes");
            Send(buffer, offset, count);
        }

        void OnSslDecryptedByte(Byte value)
        {
            Log.Debug("1 byte");
            SendByte(value);
        }

        bool Send(Byte[] buffer, Int32 offset, Int32 count)
        {
            _SendStream.Write(buffer, offset, count);
            Send();
            return true;
        }

        bool SendByte(Byte value)
        {
            _SendStream.WriteByte(value);
            Send();
            return true;
        }

        public virtual bool SendMessage<T>(T msg) where T: class
        {
            byte[] buffer = ProtobufSerializer.Serialize<T>(msg);
            return Send(buffer, 0, buffer.Length);
        }

        protected void SendEvent_Completed(object sender, SocketAsyncEventArgs arg)
        {
            _SendStream.Skip(SendEventArgs.BytesTransferred);
            Interlocked.Exchange(ref _SendLocked, 0);
            Send();
        }

        void Send()
        {
            bool SendAsync = false;
            if (Interlocked.CompareExchange(ref _SendLocked, 1, 0) != 1)
            {
                while (_SendStream.DataAvailable)
                {
                    int bytes = _SendStream.Peek(_SendBuffer, 0, _SendBuffer.Length);
                    if (bytes <= 0) break;
                    SendEventArgs.SetBuffer(_SendBuffer, 0, bytes);
                    if (!_ChannelSocket.SendAsync(SendEventArgs))
                    {//Send同步完成
                        _SendStream.Skip(SendEventArgs.BytesTransferred);
                    }
                    else
                    { 
                        SendAsync = true;
                        break;
                    }
                }
            }
            if (!SendAsync)
            {
                Interlocked.Exchange(ref _SendLocked, 0);
            }
        }

        public void Close()
        {
            if (_ChannelSocket != null) _ChannelSocket.Shutdown(SocketShutdown.Both);
            _ChannelSocket = null;
        }
    }
}
