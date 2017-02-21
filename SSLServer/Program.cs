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

namespace SSLServer
{
    class Program
    {
        static X509Certificate serverCertificate = null;
        static SocketStream _SocketStream;
        static SslStream _SslStream;
        static byte[] ReadBuffer = new byte[4096];
        
        public static void RunServer()
        {
            _SocketStream = new SocketStream();
            _SslStream = new SslStream(_SocketStream);
            _SslStream.ReadTimeout = 5000;
            _SslStream.WriteTimeout = 5000;
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
            try
            {
                _SslStream.BeginAuthenticateAsServer(serverCertificate, AuthenticateCallback, null);
                NetworkStream stream = client.GetStream();
                //stream.BeginRead(ReadBuffer,0, ReadBuffer.Length,)


                Console.WriteLine("Waiting for client message...");

                string messageData = ReadMessage(_SslStream);
                Console.WriteLine("Received: {0}", messageData);
                byte[] message = Encoding.UTF8.GetBytes("Hello from the server.");
                Console.WriteLine("Sending hello message.");
                _SslStream.Write(message);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                _SslStream.Close();
                client.Close();
                return;
            }
            finally
            {
                _SslStream.Close();
                client.Close();
            }
        }

        static private void AuthenticateCallback(IAsyncResult ar)
        {
            try
            {
                _SslStream.EndAuthenticateAsServer(ar);
            }
            catch (AuthenticationException e)
            {
                return;
            }
            catch (IOException e)
            {
                return;
            }
        }


        static string ReadMessage(SslStream _SslStream)
        {
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            do
            {
                bytes = _SslStream.Read(buffer, 0, buffer.Length);
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

        static readonly Object _syncRoot = new object();

        static void TestLock()
        {
            lock (_syncRoot)
            {
                Console.WriteLine("TestLock towice");
            }
        }

        static void TestByteStream()
        {
            ByteStream bs = new ByteStream(7);
            for (int i = 'a'; i <= 'z'; i++)
            {
                bs.WriteByte((byte)i);
            }
            bs.WriteByte((byte)'\n');
            string hello = ". Hello World\n";
            byte[] bytes = Encoding.ASCII.GetBytes(hello);
            for (int i = 0; i < 5; i++)
            {
                bs.WriteByte((byte)('1' + i));
                bs.Write(bytes, 0, bytes.Length);
            }
            byte[] buf = new byte[13];
            do
            {
                int b = bs.ReadByte();
                if (b == -1) break;
                buf[0] = (byte)b;
                int len = bs.Read(buf, 1, buf.Length-1);
                len++;
                string str = Encoding.ASCII.GetString(buf, 0, len);
                Console.Write(str);
                if (len == 1) break;
            } while (true);
        }

        static void TestServer()
        {
            TcpMessageServerSettings ServerSettings = new TcpMessageServerSettings()
            {
                MaxChannels = 1,
                ListenPort = 1234,
                UseSSL = true,
                CertificateFile = "E:\\MessageFramework\\SelfCert.pfx",
            };
            ServerSettings.ChannelSettings.SendBufferSize = 4096;
            ServerSettings.ChannelSettings.RecvBufferSize = 4096;
            ServerSettings.ChannelSettings.ConnectTimeout = 100;
            ServerSettings.ChannelSettings.SendTimeout = 1000;
            ServerSettings.ChannelSettings.ReceiveTimeout = 1000;
            ServerSettings.ChannelSettings.ChannelTimeout = 5000;

            TcpMessageServer MessageServer = new TcpMessageServer(ServerSettings);
            MessageServer.Start();
            Log.Debug("Message server start at port " + ServerSettings.ListenPort);
        }

        public static void Main(string[] args)
        {

            TestByteStream();
            TestServer();
            do
            {
                int key = Console.Read();
                if (key == 'q') break;
            } while (true);
            return;

            MsgUser user = new MsgUser();
            user.from = "ahai";
            user.to = "gca";
            user.username = "ahai.ysh";
            user.role = "admin";
            byte[] msg = ProtobufSerializer.Serialize(user);
            MsgUser user1 = (MsgUser)ProtobufSerializer.Deserialize(msg, 0, msg.Length);

            MsgLogin login = new MsgLogin("Hello world");
            login.from = "ahai";
            login.to = "gca";
            login.username = "username";
            login.password = "password";

            byte[] msg2 = ProtobufSerializer.Serialize(login);
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

