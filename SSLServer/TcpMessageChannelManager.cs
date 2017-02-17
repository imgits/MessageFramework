using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace SSLServer
{
    class TcpMessageChannelManager
    {
        private Stack<TcpMessageChannel> _FreeChannelPool;
        private List<TcpMessageChannel> _UsedChannelList;
        TcpMessageChannelSettings _ChannelSettings;
        private byte[] _ReceiveBufferPool;
        int     _TotalChannels;

        public TcpMessageChannelManager(TcpMessageChannelSettings settings)
        {
            _FreeChannelPool = new Stack<TcpMessageChannel>(settings.MaxChannels);
            _UsedChannelList = new List<TcpMessageChannel>(settings.MaxChannels);

            _ChannelSettings = settings;
            _TotalChannels = 0;
            _ReceiveBufferPool = new byte[settings.MaxChannels * settings.ReceiveBufferSize];
        }

        public void Release(TcpMessageChannel item)
        {
            if (item == null)
            {
                throw new ArgumentException("Items added to a AsyncSocketUserToken cannot be null");
            }
            item.Close();
            lock (_FreeChannelPool)
            {
                _UsedChannelList.Remove(item);
                _FreeChannelPool.Push(item);
            }
        }

        public TcpMessageChannel Allocate()
        {
            lock (_FreeChannelPool)
            {
                TcpMessageChannel channel = null;
                if (_FreeChannelPool.Count > 0)
                {
                    channel = _FreeChannelPool.Pop();
                }
                else if (_TotalChannels >= _ChannelSettings.MaxChannels)
                {
                    if (CloseIdleChannels()) channel = _FreeChannelPool.Pop();
                }
                else
                {
                    channel = CreateChannel(_TotalChannels++);
                }
                if (channel != null)
                {
                    _UsedChannelList.Add(channel);
                }
                return channel;
            }
        }

        internal TcpMessageChannel CreateChannel(int id)
        {
            var channel = new TcpMessageChannel();
            channel.ReceiveBuffer = _ReceiveBufferPool;
            channel.ReceiveBufferOffset = id * _ChannelSettings.ReceiveBufferSize;
            channel.ReceiveBufferSize = _ChannelSettings.ReceiveBufferSize;
            channel.ChannelId = id;
            return channel;
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
