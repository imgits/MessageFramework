using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

namespace MessageFramework
{
    class Program
    {
        static ServerSettings LoadAppConfig()
        {
            ServerSettings _ServerSettings = new ServerSettings();
            foreach (string key in ConfigurationManager.AppSettings)
            {
                string value = ConfigurationManager.AppSettings[key];
                switch (key)
                {
                    case "MaxChannels": _ServerSettings.MaxChannels = int.Parse(value); break;
                    case "ListenPort": _ServerSettings.ListenPort = int.Parse(value); break;
                    case "UseSSL": _ServerSettings.UseSSL = bool.Parse(value); break;
                    case "SendBufferSize": _ServerSettings.SendBufferSize = int.Parse(value); break;
                    case "RecvBufferSize": _ServerSettings.RecvBufferSize = int.Parse(value); break;
                    case "ConnectTimeout": _ServerSettings.ConnectTimeout = int.Parse(value); break;
                    case "RecvTimeout": _ServerSettings.RecvTimeout = int.Parse(value); break;
                    case "SendTimeout": _ServerSettings.SendTimeout = int.Parse(value); break;
                    case "HeartBeatPeriod": _ServerSettings.SendTimeout = int.Parse(value); break;
                    case "X509Certificate":
                        _ServerSettings.Certificate = new X509Certificate2(value, "messageframework");
                        break;

                }
                Console.WriteLine("Key: {0}, Value: {1}", key, value);
            }
            return _ServerSettings;
        }

        static void DoTcpMessageServerTest()
        {
            ServerSettings _ServerSettings = LoadAppConfig();
            TcpMessageServerTest Server = new TcpMessageServerTest(_ServerSettings);
            Server.Start();
        }

        public static void Main(string[] args)
        {
            DoTcpMessageServerTest();
            return;
            //do
            //{
            //    int key = Console.Read();
            //    if (key == 'q') break;
            //} while (true);
            //return;

            MsgUser user = new MsgUser()
            {
                from = "ahai",
                to = "gca",
                username = "ahai.ysh",
                role = "admin"
            };
            byte[] msg = ProtobufSerializer.Serialize(user);
            MsgUser user1 = ProtobufSerializer.Deserialize(msg, 0, msg.Length) as MsgUser;

            MsgLogin login = new MsgLogin("ahai","ysh19680215")
            {
                from = "ahai",
                to = "gca",
            };

            byte[] msg2 = ProtobufSerializer.Serialize(login);
            MsgLogin login2 = (MsgLogin)ProtobufSerializer.Deserialize(msg2, 0, msg2.Length);

            return;
        }
    }
}

