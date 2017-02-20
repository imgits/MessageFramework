using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SslServerStreamTest
{
    class Program
    {
        static void ClientThread()
        {
            SslClient client = new SslClient();
            client.Start("127.0.0.1", 1234);
        }

        static void Main(string[] args)
        {
            Thread t = new Thread(ClientThread);
            t.Start();
            //t.Join();
            //Thread.Sleep(1000);
            SslServer server = new SslServer(1234);
            server.Start();
        }
    }
}
