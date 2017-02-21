using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    class TcpMessageServerSettings
    {
        public int      MaxChannels { get; set; }
        public int      ListenPort { get; set; }
        public bool     UseSSL { get; set; }
        public string   CertificateFile { get; set; }
        public ChannelSettings ChannelSettings = new ChannelSettings();
    }

    class ChannelSettings
    {
        public int SendBufferSize { get; set; }
        public int RecvBufferSize { get; set; }
        public int ConnectTimeout { get; set; }
        public int ReceiveTimeout { get; set; }
        public int SendTimeout { get; set; }
        public int ChannelTimeout { get; set; }
    }
}
