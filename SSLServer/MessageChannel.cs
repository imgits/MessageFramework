using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    abstract class MessageChannel
    {
        public SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        public SocketAsyncEventArgs SendEventArgs { get; set; }
        public byte[] ReceiveBuffer;
        public int ReceiveBufferOffset;
        public int ReceiveBufferSize;
        public DateTime ActiveDateTime;
        public int ChannelId { get; set; }

        protected Socket _ChannelSocket { get; set; }
        protected bool _IsServerChannel;
        protected int _ReceivedBytes;
        protected int _SendLocked = 0;
        protected ConcurrentQueue<object> _SendMessageQueue;

    }
}
