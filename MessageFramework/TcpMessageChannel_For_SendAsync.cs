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
using SecStream;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MessageFramework
{
    public class TcpMessageChannel
    {
        protected SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        protected SocketAsyncEventArgs SendEventArgs { get; set; }

        public event EventHandler<MessageHeader> OnMessageReceived;

        public    DateTime  ActiveDateTime;
        public    int       ChannelId       { get; set;}
        public    Guid      ChannelGuid     { get; set; }
        public    string    ChannelName     { get; set; }
        public    bool      IsAuthenticated { get; set; }
        protected Socket    _ChannelSocket  { get; set; }
        protected int       _SendLocked = 0;

        readonly ChannelSettings _Settings;

        protected readonly ByteStream  _SendStream;
        protected readonly ByteStream  _RecvStream;
        protected readonly byte[]      _SendBuffer;
        protected readonly byte[]      _RecvBuffer;

        Dictionary<long, object> SendRecvMessages = new Dictionary<long, object>();
        

        public TcpMessageChannel(int id, ChannelSettings Settings)
        {
            ChannelId = id;
            _Settings = Settings;
            _ChannelSocket = null;
            ReceiveEventArgs = new SocketAsyncEventArgs();
            SendEventArgs = new SocketAsyncEventArgs();
            ReceiveEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveEvent_Completed);
            SendEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(SendEvent_Completed);
            ActiveDateTime = DateTime.Now;

            _SendStream = new ByteStream();
            _RecvStream = new ByteStream();
            _SendBuffer = new byte[Settings.SendBufferSize];
            _RecvBuffer = new byte[Settings.RecvBufferSize];
            _SendLocked = 0;

            OnMessageReceived = null;
        }

        public virtual void Start(Socket ClientSocket)
        {
            _ChannelSocket = ClientSocket;
            SetKeepAlive(_Settings.HeartBeatPeriod);
            StartReceive();
        }

        protected void StartReceive()
        {
            ReceiveEventArgs.SetBuffer(_RecvBuffer, 0, _RecvBuffer.Length);
            if (!_ChannelSocket.ReceiveAsync(ReceiveEventArgs))
            {
                Log.Debug("ChannelSocket.ReceiveAsync 事件已同步完成");
                ProcessReceive(ReceiveEventArgs);
            }
        }

        void ReceiveEvent_Completed(object sender, SocketAsyncEventArgs ReceiveEventArgs)
        {
            //Log.Debug("ChannelSocket.ReceiveAsync 异步事件完成");
            ProcessReceive(ReceiveEventArgs);
        }

        protected virtual void ProcessReceive(SocketAsyncEventArgs ReceiveEventArgs)
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
            _RecvStream.Write(_RecvBuffer, 0, ReceiveEventArgs.BytesTransferred);
            ProcessReceivedData();
            StartReceive();
        }

        protected void ProcessReceivedData()
        {
            while (_RecvStream.Length >= 2)
            {
                //packet_size包含自身的2字节长度
                int bytes = _RecvStream.Peek(_RecvBuffer, 0, 2);
                if (bytes != 2) break;
                int packet_size = BitConverter.ToUInt16(_RecvBuffer, 0);
                if (_RecvStream.Length >= packet_size)
                {
                    byte[] msgbuf = new byte[packet_size];
                    bytes = _RecvStream.Read(msgbuf, 0, packet_size);
                    if (bytes != packet_size) break;
                    ProcessReceivedPacket(msgbuf, 0, packet_size);
                }
            }
        }

        protected void ProcessReceivedPacket(byte[] buffer, int offset, int count)
        {
            MessageHeader msghdr = ProtobufSerializer.Deserialize(buffer, offset, count);
            if (msghdr == null) return ;
            if (_Settings.AuthenticationRequired && !IsAuthenticated)
            {
                //if (msghdr is MessageHeader)
            }
            long ackid = msghdr.ackid;
            if (SendRecvMessages.ContainsKey(ackid))
            {
                ManualResetEvent WaitEvent = SendRecvMessages[ackid] as ManualResetEvent;
                SendRecvMessages[ackid] = msghdr;
                WaitEvent.Set();
            }
            else if (OnMessageReceived!=null)
            {
                OnMessageReceived(this, msghdr);
            }
        }

        protected virtual bool Send(Byte[] buffer, Int32 offset, Int32 count)
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

        /// <summary>
        /// Send a message and waiting for a response
        /// </summary>
        /// <typeparam name="T">Type of sending message</typeparam>
        /// <param name="msg">message for sending</param>
        /// <param name="timeout">Milliseconds for waiting</param>
        /// <returns>Response message with type of MessageHeader</returns>
        public MessageHeader SendRecvMessage<T>(T msg, int timeout=0) where T : class 
        {
            return SendRecvMessage<T,MessageHeader>(msg,  timeout);
        }

        /// <summary>
        /// Send a message and waiting for a response
        /// </summary>
        /// <typeparam name="T1">Type of sending message</typeparam>
        /// <typeparam name="T2">Type of response message</typeparam>
        /// <param name="msg">message for sending</param>
        /// <param name="timeout">Milliseconds for waiting</param>
        /// <returns>Response message with type of T2</returns>
        public T2 SendRecvMessage<T1, T2>(T1 msg, int timeout=0) where T1 : class where T2 : class
        {
            if (timeout <= 0) timeout = _Settings.SendTimeout + _Settings.ReceiveTimeout;
            T2 ReceivedMsg = null;
            long msgid = (msg as MessageHeader).id;
            try
            {
                byte[] buffer = ProtobufSerializer.Serialize<T1>(msg);
                ManualResetEvent WaitEvent = new ManualResetEvent(false);
                SendRecvMessages[msgid] = WaitEvent;
                if (Send(buffer, 0, buffer.Length))
                {
                    if (WaitEvent.WaitOne(timeout))
                    {
                        ReceivedMsg = SendRecvMessages[msgid] as T2;
                    }
                }
            }
            catch (Exception ex)
            {
            }
            if (SendRecvMessages.ContainsKey(msgid)) SendRecvMessages.Remove(msgid);
            return ReceivedMsg;
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

        /// <summary>
        /// 设置会话的心跳包
        /// </summary>
        /// <param name="socket">Current socket instance</param>
        /// <param name="keepAliveInterval">Specifies how often TCP repeats keep-alive transmissions when no response is received. TCP sends keep-alive transmissions to verify that idle connections are still active. This prevents TCP from inadvertently disconnecting active lines.</param>
        /// <param name="keepAliveTime">Specifies how often TCP sends keep-alive transmissions. TCP sends keep-alive transmissions to verify that an idle connection is still active. This entry is used when the remote system is responding to TCP. Otherwise, the interval between transmissions is determined by the value of the keepAliveInterval entry.</param>
        /// http://stackoverflow.com/questions/37481852/how-does-socket-keep-alive-extension-work-c-sharp
        /// http://blog.csdn.net/kenkao/article/details/5415159

        struct tcp_keepalive
        {
            uint onoff; //是否启用Keep-Alive
            uint keepalivetime; //多长时间后开始第一次探测（单位：毫秒）
            uint keepaliveinterval; //探测时间间隔（单位：毫秒）
        };

        public bool SetKeepAlive(int keepAlivePeriod)
        {
            if (this._ChannelSocket ==null || keepAlivePeriod <= 0)
            {
                return false;
            }
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];//12个字节
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);//是否启用Keep-Alive
            BitConverter.GetBytes((uint)keepAlivePeriod).CopyTo(inOptionValues, Marshal.SizeOf(dummy));//多长时间开始第一次探测
            BitConverter.GetBytes((uint)keepAlivePeriod).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);//探测时间间隔

            try
            {
                _ChannelSocket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
                return true;
            }
            catch (NotSupportedException)
            {
                _ChannelSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, inOptionValues);
                return true;
            }
            catch (NotImplementedException)
            {
                _ChannelSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, inOptionValues);
                return true;
            }
            catch (Exception)
            {
                
            }
            return false;
        }

        public void Disconnect()
        {
            if (_ChannelSocket != null)
            {
                _ChannelSocket.Disconnect(true);
            }
        }

        public void Close()
        {
            if (_ChannelSocket != null)
            {
                _ChannelSocket.Shutdown(SocketShutdown.Both);
                _ChannelSocket.Close();
            }
            _ChannelSocket = null;
        }
    }
}
