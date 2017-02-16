using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    class TcpMessageServerSettings
    {
        public int      ListenPort { get; set; }
        public bool     UseSSL { get; set; }
        public string   CertificateFile { get; set; }
        TcpMessageChannelSettings _ChannelSettings = new TcpMessageChannelSettings();
        public TcpMessageChannelSettings ChannelSettings
        {
            get { return _ChannelSettings; }
        }
    }

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
