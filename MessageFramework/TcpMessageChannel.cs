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

namespace MessageFramework
{

    public class TcpMessageChannel
    {
        protected SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        protected SocketAsyncEventArgs SendEventArgs { get; set; }

        public event EventHandler<MessageHeader> OnMessageReceived;

        public    DateTime ActiveDateTime;
        public    int ChannelId { get; set;}

        protected Socket    _ChannelSocket { get; set; }
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
            StartReceive();
        }

        protected bool StartReceive()
        {
            ReceiveEventArgs.SetBuffer(_RecvBuffer, 0, _RecvBuffer.Length);
            if (!_ChannelSocket.ReceiveAsync(ReceiveEventArgs))
            {
                Log.Debug("ChannelSocket.ReceiveAsync 事件已同步完成");
                ProcessReceive(ReceiveEventArgs);
            }
            return true;
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
            //while(_RecvStream.DataAvailable)
            //{
            //    int bytes = _RecvStream.Read(_RecvBuffer, 0, _RecvBuffer.Length);
            //    if (bytes <= 0) break;
            //    string msg = Encoding.ASCII.GetString(_RecvBuffer, 0, bytes);
            //    Console.Write(msg);
            //    Send(_RecvBuffer, 0, bytes);
            //}
            //return;

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

        public MessageHeader SendRecvMessage<T>(T msg, int timeout) where T : class 
        {
            return SendRecvMessage<T,MessageHeader>(msg,  timeout);
        }

        public T2 SendRecvMessage<T1, T2>(T1 msg, int timeout) where T1 : class where T2 : class
        {
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
