using SecStream;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SslServerStreamTest
{
    class ClientSSL
    {
        Socket   _ClientSocket;
        StreamSSL _StreamSSL;
        public ClientSSL()
        {
            _ClientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            _StreamSSL = new StreamSSL();
            _StreamSSL.EncryptOutput = OnSslEncryptedData;
            _StreamSSL.DecryptDataOutput = OnSslDecryptedData;
        }

        /// <summary>
        /// 
        /// </summary>
        void ThreadReceive()
        {
            byte[] buffer = new byte[4096];
            do
            {
                int bytes = _ClientSocket.Receive(buffer);
                if (bytes <= 0) break;
                _StreamSSL.Decrypt(buffer, 0, bytes);
            }while (true);
        }

        public bool Start(string host, int port)
        {
            try
            {
                _ClientSocket.Connect(host, port);
                Console.WriteLine("Client connected.");
                if (!_StreamSSL.Initialize("localhost", SslProtocols.Tls12)) return false;
                
                new Thread(ThreadReceive).Start();

                byte[] messsage = Encoding.UTF8.GetBytes("Hello from the client.<EOF>\n");
                // Send hello message to the server. 
                _StreamSSL.Encrypt(messsage,0,messsage.Length);
                do
                {
                    string msg = Console.ReadLine() + "\n";
                    // Send hello message to the server. 
                    //messsage = Encoding.UTF8.GetBytes(msg);
                    _StreamSSL.Encrypt(msg, Encoding.UTF8);
                    if (msg == "exit") break;
                } while (true);
                // Close the client connection.
                _ClientSocket.Close();
                Console.WriteLine("Client closed.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex.Message);
                Close();
            }
            return false;
        }

        void Close()
        {
            _ClientSocket.Shutdown(SocketShutdown.Both);
            _ClientSocket.Close();
        }

        bool OnSslToken(byte[] buffer, int offset, int count)
        {
            int bytes = _ClientSocket.Send(buffer, offset, count, SocketFlags.None);
            return (bytes == count);
        }

        bool OnSslEncryptedData(byte[] buffer, int offset, int count)
        {
            int bytes = _ClientSocket.Send(buffer, offset, count,SocketFlags.None);
            return (bytes == count);
        }

        bool OnSslDecryptedData(byte[] buffer, int offset, int count)
        {
            //int bytes = client.Send(buffer, 0, buffer.Length, SocketFlags.None);
            string msg = Encoding.UTF8.GetString(buffer, offset, count);
            Console.Write("Echo:" + msg);
            return true;
        }
    }
}

