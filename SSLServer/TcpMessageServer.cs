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
            Certificate = null;
            if (_ServerSettings.CertificateFile != null)
            {
                Certificate = new X509Certificate2(ServerSettings.CertificateFile, "messageframework");
            }
            _ChannelManager = new TcpMessageChannelManager(ServerSettings.MaxChannels, _ServerSettings.ChannelSettings);

            TcpMessageChannel channel;
            for (int i = 0; i < ServerSettings.MaxChannels; i++)
            {
                if (ServerSettings.UseSSL) channel = new SslMessageChannel(i, _ServerSettings.ChannelSettings, Certificate);
                else channel = new TcpMessageChannel();
                _ChannelManager.Push(channel);
            }

            for (int i = 0; i < MAX_ACCEPTION_SCOKETS; i++)
            {
                AllAcceptEventArgs[i] = new SocketAsyncEventArgs();
                AllAcceptEventArgs[i].Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
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
                foreach(SocketAsyncEventArgs AcceptEventArgs in AllAcceptEventArgs)
                {
                    AcceptLoop(AcceptEventArgs);
                }
            }
            catch(Exception ex)
            {
                Close(ex);
            }
        }

        internal void AcceptLoop(SocketAsyncEventArgs AcceptEventArgs)
        {
            if (!_IsListening) return;
            try
            {
                AcceptEventArgs.AcceptSocket = null;
                if (!_ListenSocket.AcceptAsync(AcceptEventArgs))
                {//事件已同步完成
                    //Log.Debug("ListenSocket.AcceptAsync 事件已同步完成");
                    ProcessAccept(AcceptEventArgs);
                }
            }
            catch (Exception ex)
            {
                Close(ex);
            }

        }

        internal void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs acceptEventArgs)
        {
            //Log.Debug("异步事件完成");
            ProcessAccept(acceptEventArgs);
        }

        internal void ProcessAccept(SocketAsyncEventArgs AcceptEventArgs)
        {
            Log.Debug("Connect From " + AcceptEventArgs.AcceptSocket.RemoteEndPoint.ToString());
            if (AcceptEventArgs.SocketError != SocketError.Success)
            {
                if (AcceptEventArgs.AcceptSocket !=null) AcceptEventArgs.AcceptSocket.Close();
                AcceptLoop(AcceptEventArgs);
                return;
            }
            Socket ClientSocket = AcceptEventArgs.AcceptSocket;
            TcpMessageChannel channel = _ChannelManager.Pop();
            if (channel == null)
            {
                TooManyClients(ClientSocket);
            }
            else
            {
                channel.Accept(ClientSocket);
            }
            AcceptLoop(AcceptEventArgs);
        }

        internal void TooManyClients(Socket ClientSocket)
        {
            Log.Warn("连接通道太多");
            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
        }

        void Close(Exception ex)
        {
            _IsListening = false;
            Console.WriteLine(ex.Message);
            _ListenSocket.Close();
            _ListenSocket = null;
        }
    }
}
