using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SslServerStreamTest
{
    class Program
    {
        static void ClientThread()
        {
            //SslClient client = new SslClient();
            ClientSSL client = new ClientSSL();
            client.Start("127.0.0.1", 1234);
        }

        static void Main(string[] args)
        {
            Thread t = new Thread(ClientThread);
            t.Start();
            //t.Join();
            //Thread.Sleep(1000);
            ServerSSL server = new ServerSSL(1234);
            server.Start();
        }
    }
}
