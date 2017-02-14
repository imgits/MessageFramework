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

        Socket m_listen_socket;
        int m_listen_port;
        bool m_is_listening;
        public X509Certificate Certificate { get; set; }
        TcpMessageChannelManager ChannelManager;
        TcpMessageChannelSettings ChannelSettings { get; set; }
        public TcpMessageServer(int port)
        {
            m_listen_port = port;
            Certificate = null;
            m_is_listening = false;
            ChannelManager = new TcpMessageChannelManager(ChannelSettings);
        }

        public void Start()
        {
            try
            {
                m_listen_socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, m_listen_port);
                m_listen_socket.Bind(localEndPoint);
                m_listen_socket.Listen(DEFAULT_MAX_CONNECTIONS);
                AcceptLoop();
            }
            catch(Exception ex)
            {

            }
        }

        internal void AcceptLoop()
        {
            try
            {
                SocketAsyncEventArgs acceptEventArgs = new SocketAsyncEventArgs();
                acceptEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
                if (m_listen_socket.AcceptAsync(acceptEventArgs))
                {//事件已同步完成
                    ProcessAccept(acceptEventArgs);
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

        internal void ProcessAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            AcceptLoop();
            if (acceptEventArgs.SocketError != SocketError.Success)
            {
                if (acceptEventArgs.AcceptSocket !=null) acceptEventArgs.AcceptSocket.Close();
                return;
            }
            Socket ClientSocket = acceptEventArgs.AcceptSocket;
            acceptEventArgs.AcceptSocket = null;
            TcpMessageChannel channel = ChannelManager.Allocate(ClientSocket);
            if (channel == null)
            {
                TooManyClient(ClientSocket);
            }
            else
            {
                acceptEventArgs.AcceptSocket = null;
                channel.Certificate = this.Certificate;
            }
            //channel.StartReceive();
        }

        internal void TooManyClient(Socket ClientSocket)
        {

        }
    }
}
