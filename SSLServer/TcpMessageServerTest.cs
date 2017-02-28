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
        public TcpMessageServerTest(ServerSettings ServerSettings)
        {
            _ServerSettings = ServerSettings;
            _TcpMessageServer = new TcpMessageServer(_ServerSettings);
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
            text.text = text.text.ToUpper();
            (sender as TcpMessageChannel).SendMessage(text);
        }

        public void OnUserLogin(object sender, MessageHeader msghdr)
        {
            TcpMessageChannel channel = (TcpMessageChannel)sender;
            MsgLogin login = (MsgLogin)msghdr;
            Log.Debug($"OnUserRegister() user:{login.username} password:{login.password}");
            MsgText text  = new MsgText($"user {login.username} login OK");
            text.ackid = msghdr.id;
            channel.SendMessage(text);
            MsgFriendList friendlist = new MsgFriendList();
            for (int i = 0; i < 10; i++)
            {
                MsgFriend friend = new MsgFriend()
                {
                    friendid = i,
                    friendname = $"friend{i}",
                    groupname = $"group{i}",
                    join_time = DateTime.Now
                };
                friendlist.Friends.Add(friend);
            }
            channel.SendMessage(friendlist);
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
