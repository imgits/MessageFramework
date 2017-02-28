using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    public class ServerSettings : ChannelSettings
    {
        public int MaxChannels { get; set; }
        public int ListenPort { get; set; }
        public bool UseSSL { get; set; }
        public SslProtocols SslProtocol { get; set; }
        public X509Certificate2 Certificate { get; set; }
        public ServerSettings()
        {
            this.MaxChannels = 100;
            this.ListenPort = 1234;
            this.UseSSL = false;
            this.SslProtocol = SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
            this.Certificate = Certificate;

            this.SendBufferSize = 4096;
            this.RecvBufferSize = 4096;
            this.ConnectTimeout = 5000;
            this.SendTimeout = 2000;
            this.RecvTimeout = 2000;
            this.HeartBeatPeriod = 60*1000;

        }
    }
}
