using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    [ProtoContract]
    public class MsgLogin : MessageHeader
    {
        public string username { get; set; }
        public string password { get; set; }
        public string text { get; set; }
        /// <summary>
        /// 非公共域和非属性域均不进行序列化
        /// </summary>
        //private string text;
        //public string text;
        public MsgLogin()
        {
        }

        public MsgLogin(string text = null)
        {
            this.text = text;
        }
    }

    [ProtoContract]
    public class MsgText : MessageHeader
    {
        public string text { get; set; }
    }

    [ProtoContract]
    public class MsgUser : MessageHeader
    {
        public int userid { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string role { get; set; }
        public byte[] image { get; set; }
        public DateTime register_time { get; set; }
    }

    [ProtoContract]
    public class MsgFriend
    {
        public int friendid { get; set; }
        public string friendname { get; set; }
        public string groupname { get; set; }
        public byte[] image { get; set; }
        public DateTime join_time { get; set; }
    }

    [ProtoContract]
    public class MsgFriendList : MessageHeader
    {
        public int userid { get; set; }
        public string username { get; set; }
        public List<MsgFriend> Friends { get; set; }
    }
}

