using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    class TcpMessageChannelManager
    {
        private Stack<TcpMessageChannel> m_free_pool;
        private List<TcpMessageChannel> m_used_pool;
        TcpMessageChannelSettings m_channel_settings;
        private byte[] m_receive_buffers;
        int m_total_channels;
        public TcpMessageChannelManager(TcpMessageChannelSettings settings)
        {
            m_free_pool = new Stack<TcpMessageChannel>(settings.MaxChannels);
            m_used_pool = new List<TcpMessageChannel>(settings.MaxChannels);
            m_channel_settings = settings;
            m_total_channels = 0;
            m_receive_buffers = new byte[settings.MaxChannels * settings.ReceiveBufferSize];
        }

        public void Release(TcpMessageChannel item)
        {
            if (item == null)
            {
                throw new ArgumentException("Items added to a AsyncSocketUserToken cannot be null");
            }
            item.Close();
            lock (m_free_pool)
            {
                m_used_pool.Remove(item);
                m_free_pool.Push(item);
            }
        }

        public TcpMessageChannel Allocate(Socket socket)
        {
            lock (m_free_pool)
            {
                TcpMessageChannel channel = null;
                if (m_free_pool.Count > 0)
                {
                    channel = m_free_pool.Pop();
                }
                else if (m_total_channels >= m_channel_settings.MaxChannels)
                {
                    if (CloseIdleChannels()) channel = m_free_pool.Pop();
                }
                else
                {
                    channel = new TcpMessageChannel(null);
                    channel.ReceiveBuffer = m_receive_buffers;
                    channel.ReceiveBufferOffet = m_total_channels * m_channel_settings.ReceiveBufferSize;
                    channel.ReceiveBufferSize = m_channel_settings.ReceiveBufferSize;
                    channel.ChannelId = m_total_channels++;
                }
                if (channel != null)
                {
                    channel.ChannelSocket = socket;
                    m_used_pool.Add(channel);
                }
                return channel;
            }
        }

        internal bool CloseIdleChannels()
        {
            foreach(TcpMessageChannel channel in m_used_pool)
            {
                if ((DateTime.Now - channel.ActiveDateTime).Milliseconds > 1000)
                {
                    channel.Close();
                    m_free_pool.Push(channel);
                }
            }
            return (m_free_pool.Count > 0);
        }

        static public byte[] Encode(object message)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    ProtoBuf.Meta..RuntimeTypeModel.RuntimeTypeModel..Serializer..Serialize<Message>(ms, message);
                    byte[] msg = ms.ToArray();
                    int packet_size = msg.Length;
                    msg[0] = (byte)(packet_size & 0xff);
                    msg[1] = (byte)((packet_size >> 8) & 0xff);
                    return msg;
                }
            }
            catch { }
            return null;
        }

        static public Message Decode(byte[] buffer, int offset, int count)
        {
            Message msg = null;
            try
            {
                using (MemoryStream ms = new MemoryStream(buffer, offset, count))
                {
                    msg = (Message)ProtoBuf.Serializer..Deserialize(.Deserialize((ms);
                }
            }
            catch
            {
                msg = null;
            }
            return msg;
        }


    }
}
