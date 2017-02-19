using NSspi;
using NSspi.Contexts;
using NSspi.Credentials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    class TcpMessageServer
    {
        protected const int MAX_ACCEPTION_SCOKETS = 1;

        Socket  _ListenSocket;
        //int     _ListenPort;
        bool    _IsListening;
        public X509Certificate2 Certificate { get; set; }
        TcpMessageChannelManager _ChannelManager;
        TcpMessageServerSettings _ServerSettings;

        SocketAsyncEventArgs[] AllAcceptEventArgs = new SocketAsyncEventArgs[MAX_ACCEPTION_SCOKETS];

        public TcpMessageServer(TcpMessageServerSettings ServerSettings)
        {
            _ServerSettings = ServerSettings;
            Certificate = null;
            _ListenSocket = null;
            _IsListening = false;
            _ChannelManager = new TcpMessageChannelManager(ServerSettings.ChannelSettings);
            if (_ServerSettings.CertificateFile!=null)
            {
                Certificate = new X509Certificate2(ServerSettings.CertificateFile,"messageframework");
            }
        }

        public void Start()
        {
            if (_IsListening) return;
            try
            {
                _ListenSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, _ServerSettings.ListenPort);
                _ListenSocket.Bind(localEndPoint);
                _ListenSocket.Listen(MAX_ACCEPTION_SCOKETS);

                _IsListening = true;
                //同时提交多个Accept事件请求
                for(int i = 0; i < MAX_ACCEPTION_SCOKETS;i++)
                {
                    AllAcceptEventArgs[i] = new SocketAsyncEventArgs();
                    AllAcceptEventArgs[i].Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
                    AcceptLoop(AllAcceptEventArgs[i]);
                }
            }
            catch(Exception ex)
            {

            }
        }

        internal void AcceptLoop(SocketAsyncEventArgs AcceptEventArgs)
        {
            try
            {
                AcceptEventArgs.AcceptSocket = null;
                if (!_ListenSocket.AcceptAsync(AcceptEventArgs))
                {//事件已同步完成
                    Log.Debug("ListenSocket.AcceptAsync 事件已同步完成");
                    ProcessAccept(AcceptEventArgs);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        internal void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs acceptEventArgs)
        {
            Log.Debug("异步事件完成");
            ProcessAccept(acceptEventArgs);
        }

        internal void ProcessAccept(SocketAsyncEventArgs AcceptEventArgs)
        {
            if (AcceptEventArgs.SocketError != SocketError.Success)
            {
                if (AcceptEventArgs.AcceptSocket !=null) AcceptEventArgs.AcceptSocket.Close();
                AcceptLoop(AcceptEventArgs);
                return;
            }
            Socket ClientSocket = AcceptEventArgs.AcceptSocket;
            TestStreamSSL(ClientSocket);
            //TestNSspi(ClientSocket);
            return;
            TcpMessageChannel channel = _ChannelManager.Allocate();
            if (channel == null)
            {
                Log.Warn("连接通道太多");
                TooManyClients(ClientSocket);
            }
            else
            {
                channel.UseSSL = true;
                channel.Certificate = this.Certificate;
                channel.Accept(ClientSocket);
            }
            AcceptLoop(AcceptEventArgs);
        }

        internal void TooManyClients(Socket ClientSocket)
        {

        }

        void TestStreamSSL(Socket client)
        {
            NetworkStream client_stream = new NetworkStream(client);
            StreamSSL sslstream = new StreamSSL(client_stream, "localhost");

            sslstream.Authenticate(Certificate);
            byte[] buffer = new byte[1024];
            while(sslstream.IsAuthenticated)
            {
                int bytes = sslstream.Read(buffer, 0, buffer.Length);
                if (bytes>0)
                {
                    string msg = Encoding.ASCII.GetString(buffer, 0, bytes);
                    Console.Write(msg);
                    sslstream.Write(buffer, 0, bytes);
                }
            }
        }

        void TestNSspi(Socket client)
        {
            ServerCredential serverCred = null;
            ServerContext server = null;
            byte[] serverToken;
            SecurityStatus serverStatus;

            try
            {
                serverCred = new ServerCredential(PackageNames.Negotiate);

                server = new ServerContext(
                    serverCred,
                    ContextAttrib.SequenceDetect |
                    ContextAttrib.ReplayDetect |
                    ContextAttrib.Confidentiality |
                    ContextAttrib.AcceptExtendedError |
                    ContextAttrib.AllocateMemory |
                    ContextAttrib.InitStream
                );
                serverToken = null;
                byte[] buffer = new byte[4096];
                do
                {
                    int TokenSize = client.Receive(buffer);
                    if (TokenSize <= 0) break;
                    byte[] clientToken = new byte[TokenSize];
                    Buffer.BlockCopy(buffer, 0, clientToken, 0, TokenSize);
                    serverStatus = server.AcceptToken(clientToken, out serverToken);
                    if (serverStatus != SecurityStatus.ContinueNeeded) break;
                } while (true);

                do
                {
                    int DataSize = client.Receive(buffer);
                    if (DataSize <= 0) break;
                    byte[] cipherText = new byte[DataSize];
                    Buffer.BlockCopy(buffer, 0, cipherText, 0, DataSize);
                    byte[] Plaintext = server.Decrypt(cipherText);
                    if (Plaintext.Length >0)
                    {
                        string msg = Encoding.ASCII.GetString(Plaintext);
                        Console.Write(msg);
                        cipherText = server.Encrypt(Plaintext);
                        client.Send(cipherText,SocketFlags.None);
                    }

                }while (true);


            }
            catch (Exception ex)
            {

            }
        }
    }
}
