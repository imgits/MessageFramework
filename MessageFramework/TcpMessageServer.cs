
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    public class TcpMessageServer
    {
        protected const int MAX_ACCEPTION_SCOKETS = 1;

        Socket  _ListenSocket;
        bool    _IsListening;

        TcpMessageChannelManager _ChannelManager;
        ServerSettings _ServerSettings;

        public event EventHandler<MessageHeader> ClientMessageHandler;

        SocketAsyncEventArgs[] AllAcceptEventArgs = new SocketAsyncEventArgs[MAX_ACCEPTION_SCOKETS];
        Dictionary<Type, EventHandler<MessageHeader>> MessageHandlers = new Dictionary<Type, EventHandler<MessageHeader>>();

        public TcpMessageServer(ServerSettings ServerSettings, ChannelSettings ChannelSettings)
        {
            _ServerSettings = ServerSettings;
            _ListenSocket = null;
            _IsListening = false;
            ClientMessageHandler = null;

            _ChannelManager = new TcpMessageChannelManager(ServerSettings.MaxChannels, ChannelSettings);

            TcpMessageChannel channel;
            for (int i = 0; i < ServerSettings.MaxChannels; i++)
            {
                if (ServerSettings.UseSSL) channel = new SslMessageChannel(i, ChannelSettings, _ServerSettings.Certificate);
                else channel = new TcpMessageChannel(i, ChannelSettings);
                channel.OnMessageReceived += OnClientMessage;
                _ChannelManager.Push(channel);
            }

        }

        public bool Start()
        {
            if (_IsListening) return true;
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
                return true;
            }
            catch(Exception ex)
            {
                Close(ex);
            }
            return false;
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
                channel.Start(ClientSocket);
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
            Console.WriteLine(ex.Message);
            Close();
        }

        public void Close()
        {
            _IsListening = false;
            _ListenSocket.Close();
            _ListenSocket = null;
        }

        public void RegisterMessageHandler(Type msgtype, EventHandler<MessageHeader>handler)
        {
            MessageHandlers[msgtype] = handler;
        }

        public void RemoveMessageHandler(Type msgtype)
        {
            if (MessageHandlers.ContainsKey(msgtype))
            {
                MessageHandlers.Remove(msgtype);
            }
        }

        void OnClientMessage(object sender, MessageHeader msghdr)
        {
            Type msgtype = msghdr.GetType();
            if (MessageHandlers.ContainsKey(msgtype))
            {
                EventHandler<MessageHeader> MessageHandler = MessageHandlers[msgtype];
                if (MessageHandler != null)
                {
                    MessageHandler(sender, msghdr);
                    return;
                }
            }
            if (ClientMessageHandler != null)
            {
                ClientMessageHandler(sender, msghdr);
            }
        }

    }
}
