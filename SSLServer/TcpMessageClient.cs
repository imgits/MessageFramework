using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageFramework
{
    class TcpMessageClient
    {
        TcpClient   _TcpClient;
        Stream      _ClientStream;
        byte[]      _SendBuffer = new byte[4096];
        byte[]      _RecvBuffer = new byte[4096];
        ByteStream  _SendStream = new ByteStream();
        ByteStream  _RecvStream = new ByteStream();
        public event EventHandler<MessageHeader> OnMessageReceived;
        Dictionary<long, object> SendRecvMessages = new Dictionary<long, object>();
        public TcpMessageClient(int id, ChannelSettings Settings)
        {
            _TcpClient = new TcpClient();
            _ClientStream = null;
            OnMessageReceived = null;
        }

        public bool Connect(string host, int port, int timeout)
        {
            var result = _TcpClient.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeout));
            _TcpClient.EndConnect(result);
            if (!success)
            {
                _TcpClient.Close();
                return false;
            }
            _ClientStream = _TcpClient.GetStream();
            //StartReceive();
            return success;
        }

        private void StartReceive()
        {
            try
            {
                _ClientStream.BeginRead(_RecvBuffer, 0, _RecvBuffer.Length, (ar) =>
                {
                    int bytes = _ClientStream.EndRead(ar);
                    if (bytes>0)
                    {
                        _RecvStream.Write(_RecvBuffer, 0, bytes);
                    }
                    else
                    {

                    }
                }
                , null);
            }
            catch(Exception ex)
            {

            }
        }

        private void ProcessReceivedData()
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
            if (msghdr == null) return;
            long ackid = msghdr.ackid;
            if (SendRecvMessages.ContainsKey(ackid))
            {
                ManualResetEvent WaitEvent = SendRecvMessages[ackid] as ManualResetEvent;
                SendRecvMessages[ackid] = msghdr;
                WaitEvent.Set();
                return;
            }
            else if (OnMessageReceived!=null) OnMessageReceived(this,msghdr);
        }

        public bool SendMessage<T>(T msg) where T : class
        {
            try
            {
                byte[] buffer = ProtobufSerializer.Serialize<T>(msg);
                _ClientStream.Write(buffer, 0, buffer.Length);
                return true;
            }
            catch (Exception ex)
            {

            }
            return false;
        }

        public T2 SendRecvMessage<T1,T2>(T1 msg, int timeout) where T1 : class where T2 : class
        {
            T2 ReceivedMsg = null;
            long msgid = (msg as MessageHeader).id;
            try
            {
                byte[] buffer = ProtobufSerializer.Serialize<T1>(msg);
                SendRecvMessageArgs args = new SendRecvMessageArgs();
                ManualResetEvent WaitEvent = new ManualResetEvent(false);
                SendRecvMessages[msgid] = WaitEvent;
                _ClientStream.Write(buffer, 0, buffer.Length);
                if (WaitEvent.WaitOne(timeout))
                {
                    ReceivedMsg = SendRecvMessages[msgid] as T2;
                }
                SendRecvMessages.Remove(msgid);
            }
            catch (Exception ex)
            {

            }
            if(SendRecvMessages.ContainsKey(msgid)) SendRecvMessages.Remove(msgid);
            return ReceivedMsg;
        }
    }
}
