using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using StreamSSL;
using System.Security.Cryptography.X509Certificates;

namespace SslServerStreamTest
{
    class SslServer
    {
        TcpListener Listener;
        Socket client;
        SslServerStream SslStream;
        X509Certificate2 X509Cert;
        public SslServer(int port)
        {
            Listener = new TcpListener(IPAddress.Any, port);
            client = null;
            SslStream = new SslServerStream();
            SslStream.ServerTokenOutput = OnSslServerToken;
            SslStream.EncryptedDataOutput = OnSslEncryptedData;
            SslStream.DecryptedDataOutput = OnSslDecryptedData;

            X509Cert = new X509Certificate2("E:/MessageFramework/SelfCert.pfx", "messageframework");
        }

        public bool Start()
        {
            Listener.Start();
            client = Listener.AcceptSocket();
            if (!SslStream.CreateCredentials(X509Cert)) return false;
            byte[] buffer = new byte[4096];
            //认证
            do
            {
                int bytes = client.Receive(buffer, 0, buffer.Length,SocketFlags.None);
                if (bytes <= 0) break;
                if (!SslStream.AcceptClientToken(buffer, 0, bytes)) break;
            }while (!SslStream.IsAuthenticated);
            //消息
            do
            {
                int bytes = client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                if (bytes <= 0) break;
                if (!SslStream.Decrypt(buffer, 0, bytes)) break;
            }while (true);

            return false;
        }

        bool OnSslServerToken(byte[] buffer,int offset, int count)
        {
            int bytes = client.Send(buffer, 0, buffer.Length, SocketFlags.None);
            return (bytes == count);
        }

        bool OnSslEncryptedData(byte[] buffer, int offset, int count)
        {
            int bytes = client.Send(buffer, 0, buffer.Length, SocketFlags.None);
            return (bytes == count);
        }

        bool OnSslDecryptedData(byte[] buffer, int offset, int count)
        {
            //int bytes = client.Send(buffer, 0, buffer.Length, SocketFlags.None);
            string msg = Encoding.ASCII.GetString(buffer, offset, count);
            Console.Write(msg);
            SslStream.Encrypt(buffer, offset, count);
            return true;
        }
    }
}
