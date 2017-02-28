using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    public class ClientSettings : ChannelSettings
    {
        public string       Host { get; set;}
        public int          Port { get; set; }
        public bool         UseSSL { get; set;}
        public SslProtocols SslProtocol { get; set; }
        public bool         AutoReconnect { get; set; }
        public ClientSettings()
        {
            this.Host = null;
            this.Port = 0;
            this.UseSSL = false;
            this.SslProtocol = SslProtocols.Tls12;
            this.AutoReconnect = true;

            this.SendBufferSize = 4096;
            this.RecvBufferSize = 4096;
            this.ConnectTimeout = 5000;
            this.SendTimeout = 2000;
            this.RecvTimeout = 2000;
            this.HeartBeatPeriod = 60 * 1000;
        }

    }
}

