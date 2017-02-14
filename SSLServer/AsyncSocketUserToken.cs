using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    class AsyncSocketUserToken
    {
        public SocketAsyncEventArgs ReceiveEventArgs { get; set;}
        public SocketAsyncEventArgs SendEventArgs { get; set; }
        public byte[] ReceiveBuffer;
        public int ReceiveBufferOffet;
        public int ReceiveBufferSize;
        public Socket ChannelSocket { get; set; }
        public AsyncSocketUserToken(byte[] recv_buf, int offset, int size)
        {
            ReceiveBuffer = recv_buf;
            ReceiveBufferOffet = offset;
            ReceiveBufferSize = size;
        }
    }
}
