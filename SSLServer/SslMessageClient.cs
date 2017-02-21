using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    class SslMessageClient : TcpMessageChannel
    {
        public SslMessageClient(int id, ChannelSettings Settings)
            : base(id, Settings)
        {
            _ChannelSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public bool Connect(string host, int port)
        {
            var result = _ChannelSocket.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(_Settings.ConnectTimeout));
            _ChannelSocket.EndConnect(result);
            if (!success)
            {
                Close();
                return false;
            }
            StartReceive();
            return success;
        }
    }
}
