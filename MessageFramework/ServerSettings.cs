using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    public class ServerSettings
    {
        public int MaxChannels { get; set; }
        public int ListenPort { get; set; }
        public bool UseSSL { get; set; }
        public ProtocolType Protocol { get; set; }
        public X509Certificate2 Certificate { get; set; }
        public ServerSettings()
            : this(1, 1234, false, ProtocolType.Tcp, null)
        {
        }

        public ServerSettings(int MaxChannels, int ListenPort, bool UseSSL, ProtocolType Protocol, X509Certificate2 Certificate)
        {
            this.MaxChannels = MaxChannels;
            this.ListenPort = ListenPort;
            this.UseSSL = UseSSL;
            this.Protocol = Protocol;
            this.Certificate = Certificate;
        }
    }
}
