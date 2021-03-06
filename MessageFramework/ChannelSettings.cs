﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{

    public class ChannelSettings
    {
        public int SendBufferSize { get; set; }
        public int RecvBufferSize { get; set; }
        public int ConnectTimeout { get; set; }
        public int SendTimeout { get; set; }
        public int RecvTimeout { get; set; }
        public int HeartBeatPeriod { get; set; }
        
        public ChannelSettings()
            :this(4096,4096,2000,2000,2000,60*1000)
        {

        }

        public ChannelSettings(int SendBufferSize, int RecvBufferSize, int ConnectTimeout, int SendTimeout, int RecvTimeout, int HeartBeatPeriod)
        {
            this.SendBufferSize = SendBufferSize;
            this.RecvBufferSize = RecvBufferSize;
            this.ConnectTimeout = ConnectTimeout;
            this.SendTimeout = SendTimeout;
            this.RecvTimeout = RecvTimeout;
            this.HeartBeatPeriod = HeartBeatPeriod;
        }
    }
}
