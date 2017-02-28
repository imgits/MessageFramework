using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageFramework
{
    public class TcpMessageClient 
    {
        Socket _ClientSocket;
        ClientSettings      _Settings;
        TcpMessageChannel   _TcpMessageChannel;
        int                 _ReconnectTimes = 0;
        public event EventHandler<MessageHeader> ServerMessageHandler;

        Dictionary<Type, EventHandler<MessageHeader>> MessageHandlers = new Dictionary<Type, EventHandler<MessageHeader>>();

        public TcpMessageClient(ClientSettings Settings) 
        {
            _Settings = Settings;
            ServerMessageHandler = null;
            _ClientSocket = null;
            _TcpMessageChannel = new TcpMessageChannel(Settings);
            _TcpMessageChannel.OnMessageReceived += OnServerMessage;
            
        }

        protected bool Connect()
        {
            if (_ClientSocket!=null)
            {
                _ClientSocket.Shutdown(SocketShutdown.Both);
                _ClientSocket.Close();
                _ClientSocket = null;
            }
            _ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var result = _ClientSocket.BeginConnect(_Settings.Host, _Settings.Port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(_Settings.ConnectTimeout));
            _ClientSocket.EndConnect(result);
            if (!success || !_ClientSocket.Connected)
            {
                _ClientSocket.Close();
                return false;
            }
            Log.Debug("TcpMessageClient connected");
            return true;
        }

        public bool Start()
        {
            if (!Connect()) return false;
            _TcpMessageChannel.Start(_ClientSocket);
            //MsgChannelGuid MsgGuid = new MsgChannelGuid()
            //{
            //    from = "Client",
            //    to = "server"
            //};
            //MessageHeader reslut = _TcpMessageChannel.SendRecvMessage(MsgGuid);
            return true;
        }

        void OnChannelClosed(object sender, Exception ex)
        {
            Log.Debug($"ChannelClosed: {ex.Message}");
            if (_Settings.AutoReconnect)
            {

            }
        }


        public void RegisterMessageHandler(Type msgtype, EventHandler<MessageHeader> handler)
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

        public virtual bool SendMessage<T>(T msg) where T : class
        {
            return _TcpMessageChannel.SendMessage<T>(msg);
        }

        public MessageHeader SendRecvMessage<T>(T msg, int timeout=0) where T : class
        {
            return _TcpMessageChannel.SendRecvMessage<T, MessageHeader>(msg, timeout);
        }

        public T2 SendRecvMessage<T1, T2>(T1 msg, int timeout=0) where T1 : class where T2 : class
        {
            return _TcpMessageChannel.SendRecvMessage<T1,T2>(msg, timeout);
        }

        public void OnServerMessage(object sender, MessageHeader msghdr)
        {
            Type msgtype = msghdr.GetType();
            if (MessageHandlers.ContainsKey(msgtype))
            {
                EventHandler<MessageHeader> MessageHandler = MessageHandlers[msgtype];
                MessageHandler?.Invoke(sender, msghdr);
            }
            else ServerMessageHandler?.Invoke(sender, msghdr);
        }

    }
}
