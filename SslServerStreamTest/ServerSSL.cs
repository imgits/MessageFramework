using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using SecStream;
using System.Security.Cryptography.X509Certificates;

namespace SslServerStreamTest
{
    class ServerSSL
    {
        TcpListener Listener;
        Socket client;
        StreamSSL _StreamSSL;
        X509Certificate2 X509Cert;
        public ServerSSL(int port)
        {
            Listener = new TcpListener(IPAddress.Any, port);
            client = null;
            _StreamSSL = new StreamSSL()
            {
                DecryptOutput = (byte[] buffer, int offset, int count) =>
                {
                    string msg = Encoding.UTF8.GetString(buffer, offset, count);
                    Console.Write("Recv:" + msg);
                    msg = msg.ToUpper();
                    _StreamSSL.Encrypt(msg);
                    return true;
                }
            };

            X509Cert = new X509Certificate2("E:/MessageFramework/SelfCert.pfx", "messageframework");
        }

        public bool Start()
        {
            Listener.Start();
            client = Listener.AcceptSocket();
            if (!_StreamSSL.AuthenticateAsServer(client,X509Cert)) return false;
            byte[] buffer = new byte[4096];
            do
            {
                int bytes = client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                if (bytes <= 0) break;
                if (!_StreamSSL.Decrypt(buffer, 0, bytes)) break;
            }while (true);

            return false;
        }

        bool OnSslToken(byte[] buffer,int offset, int count)
        {
            int bytes = client.Send(buffer, offset, count, SocketFlags.None);
            return (bytes == count);
        }

        bool OnSslEncryptedData(byte[] buffer, int offset, int count)
        {
            int bytes = client.Send(buffer, offset, count, SocketFlags.None);
            return (bytes == count);
        }

        bool OnSslDecryptedData(byte[] buffer, int offset, int count)
        {
            //int bytes = client.Send(buffer, 0, buffer.Length, SocketFlags.None);
            string msg = Encoding.UTF8.GetString(buffer, offset, count);
            Console.Write("Recv:" + msg);
            msg = msg.ToUpper();
            _StreamSSL.Encrypt(msg);
            return true;
        }
    }
}
