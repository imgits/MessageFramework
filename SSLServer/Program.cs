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

namespace MessageFramework
{
    class Program
    {
        static void DoTcpMessageServerTest()
        {
            TcpMessageServerTest Server = new TcpMessageServerTest(1234);
            Server.Start();
        }

        static void DoTSslMessageServerTest()
        {
            TcpMessageServerTest Server = new TcpMessageServerTest(1234);
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

            MsgUser user = new MsgUser();
            user.from = "ahai";
            user.to = "gca";
            user.username = "ahai.ysh";
            user.role = "admin";
            byte[] msg = ProtobufSerializer.Serialize(user);
            MsgUser user1 = ProtobufSerializer.Deserialize(msg, 0, msg.Length) as MsgUser;

            MsgLogin login = new MsgLogin("Hello world");
            login.from = "ahai";
            login.to = "gca";
            login.username = "username";
            login.password = "password";

            byte[] msg2 = ProtobufSerializer.Serialize(login);
            MsgLogin login2 = (MsgLogin)ProtobufSerializer.Deserialize(msg2, 0, msg2.Length);

            return;
        }
    }
}

