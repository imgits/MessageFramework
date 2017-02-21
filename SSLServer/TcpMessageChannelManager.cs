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
        readonly ChannelSettings _ChannelSettings;
        readonly Object _syncRoot = new object();
        
        public TcpMessageChannelManager(int MaxChannels, ChannelSettings ChannelSettings)
        {
            _ChannelSettings = ChannelSettings;
            _FreeChannelPool = new Stack<TcpMessageChannel>(MaxChannels);
            _UsedChannelList = new List<TcpMessageChannel>(MaxChannels);
        }

        public void Push(TcpMessageChannel item)
        {
            if (item == null)
            {
                throw new ArgumentException("Items added to a AsyncSocketUserToken cannot be null");
            }
            item.Close();
            lock (_syncRoot)
            {
                _UsedChannelList.Remove(item);
                _FreeChannelPool.Push(item);
            }
        }

        public TcpMessageChannel Pop()
        {
            lock (_syncRoot)
            {
                TcpMessageChannel channel = null;
                if (_FreeChannelPool.Count > 0)
                {
                    channel = _FreeChannelPool.Pop();
                }
                else if (CloseIdleChannels())
                {
                    channel = _FreeChannelPool.Pop();
                }
                if (channel != null)
                {
                    _UsedChannelList.Add(channel);
                }
                return channel;
            }
        }

        internal bool CloseIdleChannels()
        {
            foreach(TcpMessageChannel channel in _UsedChannelList)
            {
                if ((DateTime.Now - channel.ActiveDateTime).Milliseconds > _ChannelSettings.ChannelTimeout)
                {
                    channel.Close();
                    _FreeChannelPool.Push(channel);
                }
            }
            return (_FreeChannelPool.Count > 0);
        }

    }
}
