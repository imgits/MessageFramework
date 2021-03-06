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
using SecStream;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Authentication;

namespace MessageFramework
{
    public delegate void ChannelClosedHandler(object sender,Exception ex);
    public class TcpMessageChannel
    {
        private SocketAsyncEventArgs ReceiveEventArgs { get; set; }

        public event EventHandler<MessageHeader> OnMessageReceived;
        public event ChannelClosedHandler OnChannelClosed;

        public    DateTime  ActiveDateTime;
        public    int       ChannelId       { get; set;}
        public    Guid      ChannelGuid     { get; set; }

        public    string    ChannelUserName { get; set; }
        public    ulong     ChannelUserId   { get; set; }

        private Socket    _ChannelSocket  { get; set; }

        private object    _SendLocked = new object();

        private bool _ChannelIsClosed;
        ChannelSettings   _Settings;

        ByteStream  _RecvStream;
        byte[]      _RecvBuffer;

        Dictionary<long, object> SendRecvMessages = new Dictionary<long, object>();

        //SSL相关参数
        private bool             _UseSSL;
        private bool             _AsSslClient;
        private StreamSSL        _StreamSSL;
        private string           _TargetHost;
        private SslProtocols     _SslProtocol;
        private X509Certificate2 _Certificate;

        //初始化客户端通道
        public TcpMessageChannel(ClientSettings Settings)
        {
            InitTcpChannel(-1, Settings);
            if (Settings.UseSSL) InitSslChannnel(true, null, Settings.Host, Settings.SslProtocol);
        }

        //初始化服务端通道
        public TcpMessageChannel(int id, ServerSettings Settings)
        {
            InitTcpChannel(id, Settings);
            if (Settings.UseSSL) InitSslChannnel(false, Settings.Certificate, null);
        }

        //该构造函数只能内部调用
        private void InitTcpChannel(int id, ChannelSettings Settings)
        {
            ChannelId = id;
            _Settings = Settings;
            _ChannelSocket = null;

            _ChannelIsClosed = true;
            ReceiveEventArgs = new SocketAsyncEventArgs();
            ReceiveEventArgs.Completed += (object sender, SocketAsyncEventArgs ReceiveEventArgs) =>
            {
                ProcessReceive(ReceiveEventArgs);
            };//new EventHandler<SocketAsyncEventArgs>(OnReceiveEventCompleted);
            ActiveDateTime = DateTime.Now;

            _RecvStream = new ByteStream();
            _RecvBuffer = new byte[Settings.RecvBufferSize];
            _SendLocked = 0;

            OnMessageReceived = null;

            //SSL相关参数初始化
            _UseSSL = false;
            _AsSslClient = false;
            _TargetHost = null;
            _SslProtocol = SslProtocols.None;
            _Certificate = null;
            _StreamSSL = null;
        }

        //初始化SSL通道
        private void InitSslChannnel(bool AsSslClient, X509Certificate2 Certificate, string TargetHost, SslProtocols SslProtocol= SslProtocols.None)
        {
            _UseSSL = true;
            _AsSslClient = AsSslClient;
            _TargetHost = TargetHost;
            _SslProtocol = SslProtocol;
            _Certificate = Certificate;

            _StreamSSL = new StreamSSL()
            {
                DecryptOutput = (Byte[] buffer, Int32 offset, Int32 count) =>
                {
                    _RecvStream.Write(buffer, offset, count);
                    ProcessReceivedData();
                    return true;
                }
            };
        }

        public void Start(Socket ClientSocket)
        {
            if (ClientSocket == null || !ClientSocket.Connected)
            {
                return;
            }
            _ChannelIsClosed = false;
            _ChannelSocket = ClientSocket;
            SetKeepAlive(_Settings.HeartBeatPeriod);
            if (_UseSSL)
            {
                if (_AsSslClient) _StreamSSL.AuthenticateAsClient(_ChannelSocket,_SslProtocol);
                else _StreamSSL.AuthenticateAsServer(_ChannelSocket, _Certificate);
            }
            StartReceive();
        }

        protected void StartReceive()
        {
            if (!_ChannelIsClosed)
            {
                ReceiveEventArgs.SetBuffer(_RecvBuffer, 0, _RecvBuffer.Length);
                if (!_ChannelSocket.ReceiveAsync(ReceiveEventArgs))
                {
                    Log.Debug("ChannelSocket.ReceiveAsync 事件已同步完成");
                    ProcessReceive(ReceiveEventArgs);
                }
            }
        }

        protected void ProcessReceive(SocketAsyncEventArgs ReceiveEventArgs)
        {
            if (_ChannelIsClosed) return;
            try
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
                if (_UseSSL)
                {
                    _StreamSSL.Decrypt(_RecvBuffer, 0, ReceiveEventArgs.BytesTransferred);
                }
                else
                {
                    _RecvStream.Write(_RecvBuffer, 0, ReceiveEventArgs.BytesTransferred);
                    ProcessReceivedData();
                }
            }
            catch(Exception ex)
            {
                Close(ex);
            }
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

            long ackid = msghdr.ackid;
            if (SendRecvMessages.ContainsKey(ackid))
            {
                ManualResetEvent WaitEvent = SendRecvMessages[ackid] as ManualResetEvent;
                SendRecvMessages[ackid] = msghdr;
                WaitEvent.Set();
            }
            else
            {
                OnMessageReceived?.Invoke(this, msghdr);
            }
        }

        bool SendSSL(Byte[] buffer, Int32 offset, Int32 count)
        {
            try
            {
                return _StreamSSL.Encrypt(buffer, offset, count);
            }
            catch (Exception ex)
            {
                Close(ex);
            }
            return false;
        }

        bool Send(Byte[] buffer, Int32 offset, Int32 count)
        {
            Log.Debug($"Send {count} bytes");
            if (_ChannelIsClosed) return false;
            lock (_SendLocked)
            {
                while (count > 0)
                {
                    int bytes = _ChannelSocket.Send(buffer, offset, count, SocketFlags.None);
                    if (bytes <= 0) return false;
                    offset += bytes;
                    count -= bytes;
                }
            }
            return true;
        }

        public bool SendMessage<T>(T msg) where T: class
        {
            try
            {
                byte[] buffer = ProtobufSerializer.Serialize<T>(msg);
                if (_UseSSL) return SendSSL(buffer, 0, buffer.Length);
                return Send(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Close(ex);
            }
            return false;
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
            if (timeout <= 0) timeout = _Settings.SendTimeout + _Settings.RecvTimeout;
            T2 ReceivedMsg = null;
            long msgid = (msg as MessageHeader).id;
            try
            {
                byte[] buffer = ProtobufSerializer.Serialize<T1>(msg);
                ManualResetEvent WaitEvent = new ManualResetEvent(false);
                SendRecvMessages[msgid] = WaitEvent;//注册接收等待事件
                bool SendOK = false;
                if (_UseSSL) SendOK = SendSSL(buffer, 0, buffer.Length);
                else SendOK = Send(buffer, 0, buffer.Length);
                if (SendOK)
                {
                    if (WaitEvent.WaitOne(timeout))
                    {
                        ReceivedMsg = SendRecvMessages[msgid] as T2;
                    }
                }
            }
            catch (Exception ex)
            {
                Close(ex);
            }
            if (SendRecvMessages.ContainsKey(msgid)) SendRecvMessages.Remove(msgid);
            return ReceivedMsg;
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
            catch (Exception ex)
            {
                Close(ex);    
            }
            return false;
        }

        public void Close(Exception ex=null)
        {
            Log.Debug("Channel closed");
            _ChannelIsClosed = true;
            try
            {
                if (_ChannelSocket != null && _ChannelSocket.Connected)
                {
                    _ChannelSocket.Shutdown(SocketShutdown.Both);
                    _ChannelSocket.Close();
                    _ChannelSocket.Dispose();
                }
            }
            catch { }
            _ChannelSocket = null;
            OnChannelClosed?.Invoke(this, ex);
        }
    }
}
