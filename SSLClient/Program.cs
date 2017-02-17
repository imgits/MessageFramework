﻿using System;
using System.Security;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SSLClient
{
    using System;
    using System.Collections;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Text;
    using System.Security.Cryptography.X509Certificates;
    using System.IO;
    using System.Threading;

    namespace Examples.System.Net
    {
        public class SslTcpClient
        {
            private static Hashtable certificateErrors = new Hashtable();
            // The following method is invoked by the RemoteCertificateValidationDelegate.
            public static bool ValidateServerCertificate(
                  object sender,
                  X509Certificate certificate,
                  X509Chain chain,
                  SslPolicyErrors sslPolicyErrors)
            {
                Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
                return true;
                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

                // Do not allow this client to communicate with unauthenticated servers.
                return false;
            }

            public static void RunClient(string machineName)
            {
                // Create a TCP/IP client socket.
                // machineName is the host running the server application.
                TcpClient client = new TcpClient(machineName, 901);
                Console.WriteLine("Client connected.");
                // Create an SSL stream that will close the client's stream.
                SslStream sslStream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                // The server name must match the name on the server certificate.

                //X509Store store = new X509Store(StoreName.My);
                //store.Open(OpenFlags.ReadWrite);

                //// 检索证书 
                //X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, "MyServer", false); // vaildOnly = true时搜索无结果。

                //X509CertificateCollection certs = new X509CertificateCollection();
                //X509Certificate cert = X509Certificate.CreateFromCertFile(@"D:\cashcer.cer");
                //certs.Add(cert);
                try
                {
                    sslStream.AuthenticateAsClient("localhost", null, SslProtocols.Tls, false);
                }
                catch (AuthenticationException e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    if (e.InnerException != null)
                    {
                        Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                    }
                    Console.WriteLine("Authentication failed - closing the connection.");
                    client.Close();
                    return;
                }
                // Encode a test message into a byte array.
                // Signal the end of the message using the "<EOF>".
                byte[] messsage = Encoding.UTF8.GetBytes("Hello from the client.<EOF>");
                // Send hello message to the server. 
                sslStream.Write(messsage);
                sslStream.Flush();
                string serverMessage = ReadMessage(sslStream);
                Console.WriteLine("Server says: {0}", serverMessage);
                do
                {
                    string msg = Console.ReadLine();
                    // Send hello message to the server. 
                    messsage = Encoding.UTF8.GetBytes(msg);
                    sslStream.Write(messsage);
                    sslStream.Flush();
                    // Read message from the server.
                    serverMessage = ReadMessage(sslStream);
                    Console.WriteLine("Server says: {0}", serverMessage);
                    if (msg == "exit") break;
                } while (true);
                // Close the client connection.
                client.Close();
                Console.WriteLine("Client closed.");
            }

            static string ReadMessage(SslStream sslStream)
            {
                // Read the  message sent by the server.
                // The end of the message is signaled using the
                // "<EOF>" marker.
                byte[] buffer = new byte[2048];
                StringBuilder messageData = new StringBuilder();
                int bytes = -1;
                do
                {
                    bytes = sslStream.Read(buffer, 0, buffer.Length);

                    // Use Decoder class to convert from bytes to UTF8
                    // in case a character spans two buffers.
                    Decoder decoder = Encoding.UTF8.GetDecoder();
                    char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                    decoder.GetChars(buffer, 0, bytes, chars, 0);
                    messageData.Append(chars);
                    // Check for EOF.
                    if (messageData.ToString().IndexOf("<EOF>") != -1)
                    {
                        break;
                    }
                } while (bytes != 0);

                return messageData.ToString();
            }

            private static void DisplayUsage()
            {
                Console.WriteLine("To start the client specify:");
                Console.WriteLine("clientSync machineName [serverName]");
                Environment.Exit(1);
            }

            public static void Main(string[] args)
            {
                string machineName = null;
                machineName = "127.0.0.1";
                Thread.Sleep(1000);
                try
                {
                    RunClient(machineName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                Console.ReadLine();
            }
        }
    }

}