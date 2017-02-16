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
        protected const int DEFAULT_MAX_CONNECTIONS = 10;

        Socket  _ListenSocket;
        //int     _ListenPort;
        bool    _IsListening;
        public  X509Certificate Certificate { get; set; }
        TcpMessageChannelManager _ChannelManager;
        TcpMessageServerSettings _ServerSettings;

        SocketAsyncEventArgs[] AllAcceptEventArgs = new SocketAsyncEventArgs[DEFAULT_MAX_CONNECTIONS];

        public TcpMessageServer(TcpMessageServerSettings ServerSettings)
        {
            _ServerSettings = ServerSettings;
            Certificate = null;
            _ListenSocket = null;
            _IsListening = false;
            _ChannelManager = new TcpMessageChannelManager(_ServerSettings.ChannelSettings);
            if (_ServerSettings.CertificateFile!=null)
            {
                Certificate = X509Certificate.CreateFromCertFile(_ServerSettings.CertificateFile);
            }
        }

        public void Start()
        {
            if (_IsListening) return;
            try
            {
                _ListenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, _ServerSettings.ListenPort);
                _ListenSocket.Bind(localEndPoint);
                _ListenSocket.Listen(DEFAULT_MAX_CONNECTIONS);

                _IsListening = true;
                //同时提交多个Accept事件请求
                foreach (var arg in AllAcceptEventArgs)
                {
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
                    AcceptLoop(arg);
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
                if (_ListenSocket.AcceptAsync(AcceptEventArgs))
                {//事件已同步完成
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
            
            TcpMessageChannel channel = _ChannelManager.Allocate();
            if (channel == null)
            {
                TooManyClients(ClientSocket);
            }
            else
            {
                channel.ChannelSocket = ClientSocket;
                channel.Certificate = this.Certificate;
                channel.StartReceive();
            }
            AcceptLoop(AcceptEventArgs);
        }

        internal void TooManyClients(Socket ClientSocket)
        {

        }
    }
}
