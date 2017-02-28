using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.Security.Cryptography.X509Certificates;


namespace MessageFramework
{
    class TcpMessageChannelManager
    {
        readonly Stack<TcpMessageChannel> _FreeChannelPool;
        readonly List<TcpMessageChannel> _UsedChannelList;
        readonly Object _syncRoot = new object();
        readonly int _MaxChannels = 0;
        public TcpMessageChannelManager(int MaxChannels)
        {
            _MaxChannels = MaxChannels;
            _FreeChannelPool = new Stack<TcpMessageChannel>(MaxChannels);
            _UsedChannelList = new List<TcpMessageChannel>(MaxChannels);
        }

        public void Push(TcpMessageChannel item)
        {
            if (item == null)
            {
                throw new ArgumentException("Items added to a AsyncSocketUserToken cannot be null");
            }
            lock (_syncRoot)
            {
                _UsedChannelList.Remove(item);
                _FreeChannelPool.Push(item);
                Log.Debug($"Push TcpMessageChannel {item.ChannelId}");
            }
        }

        public TcpMessageChannel Pop()
        {
            lock (_syncRoot)
            {
                if (_FreeChannelPool.Count <= 0) return null;
                TcpMessageChannel channel = _FreeChannelPool.Pop();
                _UsedChannelList.Add(channel);
                Log.Debug($"Pop TcpMessageChannel {channel.ChannelId}");
                return channel;
            }
        }

    }
}
