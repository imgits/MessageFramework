using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SslServerStreamTest
{
    class SslClient
    {
        TcpClient client;
        SslStream sslStream;
        public SslClient()
        {
            client = null;
            sslStream = null;
        }

        public bool ValidateServerCertificate(
                  object sender,
                  X509Certificate certificate,
                  X509Chain chain,
                  SslPolicyErrors sslPolicyErrors)
        {
            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
            return true;
        }

        void ReadCallback(IAsyncResult ar)
        {
            try
            {
                byte[] buffer = (byte[])ar.AsyncState;
                int recieved = sslStream.EndRead(ar);
                if (recieved > 0)
                {
                    string msg = Encoding.ASCII.GetString(buffer, 0, recieved);
                    Console.Write(msg);
                }
                sslStream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);
            }
            catch (Exception ex)
            {
            }
        }

        public bool Start(string host, int port)
        {
            try
            {
                client = new TcpClient(host, port);
                Console.WriteLine("Client connected.");
                sslStream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                byte[] buffer = new byte[4096];

                sslStream.AuthenticateAsClient("localhost", null, SslProtocols.Tls, false);
                sslStream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);

                byte[] messsage = Encoding.UTF8.GetBytes("Hello from the client.<EOF>\n");
                // Send hello message to the server. 
                sslStream.Write(messsage);
                sslStream.Flush();
                do
                {
                    string msg = Console.ReadLine() + "\n";
                    // Send hello message to the server. 
                    messsage = Encoding.UTF8.GetBytes(msg);
                    sslStream.Write(messsage);
                    sslStream.Flush();
                    if (msg == "exit") break;
                } while (true);
                // Close the client connection.
                client.Close();
                Console.WriteLine("Client closed.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex.Message);
                client.Close();
            }
            return false;
        }
    }
}
