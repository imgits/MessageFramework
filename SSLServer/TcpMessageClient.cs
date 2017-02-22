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
    class TcpMessageClient : TcpMessageChannel
    {
        public TcpMessageClient(ChannelSettings Settings, int id=0) 
            :base(id, Settings)
        {
            _ChannelSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public bool Connect(string host, int port, int timeout)
        {
            var result = _ChannelSocket.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(_Settings.ConnectTimeout));
            _ChannelSocket.EndConnect(result);
            if (!success)
            {
                _ChannelSocket.Close();
                return false;
            }
            StartReceive();
            return success;
        }
    }
}
