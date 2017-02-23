using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessageFramework;

namespace SSLClient
{
    class TcpMessageClientTest
    {
        ClientSettings _ClientSettings;
        TcpMessageClient _TcpMessageClient;
        public TcpMessageClientTest(ClientSettings ClientSettings)
        {
            _ClientSettings = ClientSettings;
            _TcpMessageClient = new TcpMessageClient(ClientSettings);
            _TcpMessageClient.ServerMessageHandler += OnServerMessage;
        }

        public void Start()
        {
            if (_TcpMessageClient.Start())
            {
                MessageLoop();
            }
            else
            {
                Console.WriteLine("TcpMessageClient start failed");
            }
        }

        void MessageLoop()
        {
            MsgLogin login = new MsgLogin();
            login.InitHeader("ahai", "gca");
            login.username = "admin";
            login.password = "guangs10029";
            MessageHeader result = _TcpMessageClient.SendRecvMessage<MsgLogin>(login, 1000);
            //if (result.result ==0)
            MsgUser user = new MsgUser();
            user.InitHeader("ahai", "gca");
            user.username = "ahai";
            user.password = "19689215";
            user.role = "admin";
            if (!_TcpMessageClient.SendMessage<MsgUser>(user))
            {
                return;
            }
            MsgText text = new MsgText();
            text.InitHeader("ahai", "gca");
            do
            {
                string msg = Console.ReadLine();
                text.text = msg;
                if (!_TcpMessageClient.SendMessage<MsgText>(text)) break;
            } while (true);
        }

        void OnServerMessage(object sender,MessageHeader msghdr)
        {
            Log.Debug($"OnServerMessage() type:{msghdr.GetType().Name}");
        }

    }
}
