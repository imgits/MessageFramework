using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    class TcpMessageServerTest
    {
        TcpMessageServer _TcpMessageServer;
        ServerSettings _ServerSettings = new ServerSettings();
        ChannelSettings _ChannelSettings = new ChannelSettings();
        X509Certificate2 Certificate;
        public TcpMessageServerTest(int port)
        {
            _ServerSettings.ListenPort = port;
            Certificate = new X509Certificate2(@"E:\MessageFramework\SelfCert.pfx", "messageframework");
            _TcpMessageServer = new TcpMessageServer(_ServerSettings, _ChannelSettings);
            _TcpMessageServer.ClientMessageHandler += OnClientMessage;
            _TcpMessageServer.RegisterMessageHandler(typeof(MsgUser), OnUserRegister);
            _TcpMessageServer.RegisterMessageHandler(typeof(MsgLogin), OnUserLogin);
            _TcpMessageServer.RegisterMessageHandler(typeof(MsgText), OnTextMessage);
        }

        public void Start()
        {
            if (_TcpMessageServer.Start())
            {
                Console.WriteLine("TcpMessageServer started");
            }
            else
            {
                Console.WriteLine("TcpMessageServer start failed");
            }
            Console.WriteLine("Process key Entry to exit");
            Console.ReadLine();
            _TcpMessageServer.Close();
        }

        public void OnTextMessage(object sender, MessageHeader msghdr)
        {
            MsgText text = (MsgText)msghdr;
            Log.Debug($"OnTextMessage() :\"{text.text}\"");
        }

        public void OnUserLogin(object sender, MessageHeader msghdr)
        {
            MsgLogin login = (MsgLogin)msghdr;
            Log.Debug($"OnUserRegister() user:{login.username} password:{login.password}");
        }

        public void OnUserRegister(object sender, MessageHeader msghdr)
        {
            MsgUser user = (MsgUser)msghdr;
            Log.Debug($"OnUserLogin() user:{user.username} password:{user.password}");
        }

        public void OnClientMessage(object sender, MessageHeader msghdr)
        {
            Log.Debug($"OnClientMessage() type:{msghdr.GetType().Name}");
        }
    }
}
