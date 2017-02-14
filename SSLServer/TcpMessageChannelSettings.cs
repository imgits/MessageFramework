using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    class TcpMessageChannelSettings
    {
        public int MaxChannels { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int ConnectTimeout { get; set; }
        public int ReceiveTimeout { get; set; }
        public int SendTimeout { get; set; }
        public int ChannelTimeout { get; set; }
    }
}
