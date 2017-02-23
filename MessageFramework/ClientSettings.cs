using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    public class ClientSettings : ChannelSettings
    {
        public string   Host { get; set;}
        public int      Port { get; set; }
        public bool     UseSSL { get; set;}

        public ClientSettings()
        {
            this.Host = null;
            this.Port = 0;
            this.UseSSL = false;
        }

        public ClientSettings(string Host, int port, bool UseSSL,int SendBufferSize, int RecvBufferSize, int ConnectTimeout, int SendTimeout, int ReceiveTimeout, int ChannelTimeout)
            :base(SendBufferSize, RecvBufferSize, ConnectTimeout, SendTimeout, ReceiveTimeout, ChannelTimeout)
        {
            this.Host = Host;
            this.Port = Port;
            this.UseSSL = UseSSL;
        }
    }
}

