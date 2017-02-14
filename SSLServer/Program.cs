using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    class Program
    {
        static X509Certificate serverCertificate = null;

        public static void RunServer()
        {
            //serverCertificate = X509Certificate.CreateFromSignedFile(@"C:\Program Files\Microsoft Visual Studio 8\SDK\v2.0\samool.pvk");
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 901);
            listener.Start();
            while (true)
            {
                try
                {
                    Console.WriteLine("Waiting for a client to connect...");
                    TcpClient client = listener.AcceptTcpClient();
                    ProcessClient(client);
                }
                catch
                {

                }
            }
        }

        static void ProcessClient(TcpClient client)
        {
            SslStream sslStream = new SslStream(client.GetStream(), false);
            try
            {
                sslStream.AuthenticateAsServer(serverCertificate, false, SslProtocols.Tls, false);
                DisplaySecurityLevel(sslStream);
                DisplaySecurityServices(sslStream);
                DisplayCertificateInformation(sslStream);
                DisplayStreamProperties(sslStream);

                sslStream.ReadTimeout = 5000;
                sslStream.WriteTimeout = 5000;
                Console.WriteLine("Waiting for client message...");
                string messageData = ReadMessage(sslStream);
                Console.WriteLine("Received: {0}", messageData);
                byte[] message = Encoding.UTF8.GetBytes("Hello from the server.");
                Console.WriteLine("Sending hello message.");
                sslStream.Write(message);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                sslStream.Close();
                client.Close();
                return;
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }

        static string ReadMessage(SslStream sslStream)
        {
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            do
            {
                bytes = sslStream.Read(buffer, 0, buffer.Length);
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                if (messageData.ToString().IndexOf("") != -1)
                {
                    break;
                }
            }
            while (bytes != 0);

            return messageData.ToString();
        }

        static void DisplaySecurityLevel(SslStream stream)
        {
            Console.WriteLine("Cipher: {0} strength {1}", stream.CipherAlgorithm, stream.CipherStrength);
            Console.WriteLine("Hash: {0} strength {1}", stream.HashAlgorithm, stream.HashStrength);
            Console.WriteLine("Key exchange: {0} strength {1}", stream.KeyExchangeAlgorithm, stream.KeyExchangeStrength);
            Console.WriteLine("Protocol: {0}", stream.SslProtocol);
        }

        static void DisplaySecurityServices(SslStream stream)
        {
            Console.WriteLine("Is authenticated: {0} as server? {1}", stream.IsAuthenticated, stream.IsServer);
            Console.WriteLine("IsSigned: {0}", stream.IsSigned);
            Console.WriteLine("Is Encrypted: {0}", stream.IsEncrypted);
        }

        static void DisplayStreamProperties(SslStream stream)
        {
            Console.WriteLine("Can read: {0}, write {1}", stream.CanRead, stream.CanWrite);
            Console.WriteLine("Can timeout: {0}", stream.CanTimeout);
        }

        static void DisplayCertificateInformation(SslStream stream)
        {
            Console.WriteLine("Certificate revocation list checked: {0}", stream.CheckCertRevocationStatus);

            X509Certificate localCertificate = stream.LocalCertificate;
            if (stream.LocalCertificate != null)
            {
                Console.WriteLine("Local cert was issued to {0} and is valid from {1} until {2}.",
                localCertificate.Subject,
                    localCertificate.GetEffectiveDateString(),
                    localCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Local certificate is null.");
            }
            X509Certificate remoteCertificate = stream.RemoteCertificate;
            if (stream.RemoteCertificate != null)
            {
                Console.WriteLine("Remote cert was issued to {0} and is valid from {1} until {2}.",
                    remoteCertificate.Subject,
                    remoteCertificate.GetEffectiveDateString(),
                    remoteCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Remote certificate is null.");
            }
        }

        private static void DisplayUsage()
        {
            Console.WriteLine("To start the server specify:");
            Console.WriteLine("serverSync certificateFile.cer");
            //Environment.Exit(1);
        }

        public static void Main(string[] args)
        {
            MsgUser user = new MsgUser();
            user.from = "ahai";
            user.to = "gca";
            user.type = ProtobufSerializer.MsgTypeId(typeof(MsgUser));
            user.username = "ahai.ysh";
            user.role = "admin";
            byte[] msg = ProtobufSerializer.Encode(user);
            MsgUser user1 = ProtobufSerializer.Decode<MsgUser>(msg, 0, msg.Length);

            byte[] msg1 = ProtobufSerializer.Serialize(user);
            MsgUser user2 = ProtobufSerializer.Deserialize<MsgUser>(msg1, 0, msg1.Length);

            MsgLogin login = new MsgLogin("Hello world");
            login.from = "ahai";
            login.to = "gca";
            login.type = ProtobufSerializer.MsgTypeId(typeof(MsgUser));
            login.username = "username";
            login.password = "password";

            byte[] msg2 = ProtobufSerializer.Serialize(login);
            MsgLogin login1 = ProtobufSerializer.Deserialize<MsgLogin>(msg2, 0, msg2.Length);

            MsgLogin login2 = (MsgLogin)ProtobufSerializer.Deserialize(msg2, 0, msg2.Length);

            return;
            //string certificate = null;
            //if (args == null || args.Length < 1)
            //{
            //    DisplayUsage();
            //}
            //certificate = args[0];
            try
            {
                X509Store store = new X509Store(StoreName.My);
                store.Open(OpenFlags.ReadWrite);

                // 检索证书 
                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, "MyServer", false); // vaildOnly = true时搜索无结果。
                if (certs.Count == 0) return;

                serverCertificate = certs[0];
                RunServer();
                store.Close(); // 关闭存储区。
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.ReadLine();
            //return 0;



            //try
            //{
            //    Console.WriteLine("服务端输出：" + ServiceSecurityContext.Current.PrimaryIdentity.AuthenticationType);
            //    Console.WriteLine(ServiceSecurityContext.Current.PrimaryIdentity.Name);
            //    Console.WriteLine("服务端时间：" + DateTime.Now.ToString());
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.Message);
            //}
            //Console.ReadLine();

        }
    }
}

