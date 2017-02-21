using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    [ProtoContract]
    public class MessageHeader
    {
        [ProtoMember(1, IsRequired = true)]
        public long id { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public long ackid { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public int type { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public bool sync { get; set; }

        [ProtoMember(5, IsRequired = false)]
        public string from { get; set; }

        [ProtoMember(6, IsRequired = false)]
        public string to { get; set; }

        [ProtoMember(7, IsRequired = false)]
        public int errorcode { get; set; }

        public string TypeName { get; set; }

        public MessageHeader()
            : this(0, 0, false, null, null, 0)
        {
        }
        public MessageHeader(MessageHeader msghdr)
            : this(msghdr.ackid, msghdr.type, msghdr.sync, msghdr.from, msghdr.to, msghdr.errorcode)
        {
        }

        public MessageHeader(int Type)
            : this(0, Type, false, null, null, 0)
        {
        }

        public MessageHeader(int Type, string To)
            : this(0, Type, false, null, To, 0)
        {
        }

        public MessageHeader(int Type, string From, string To)
            : this(0, Type, false, From, To, 0)
        {
        }

        public MessageHeader(long AckId, int Type, bool Sync, string From, string To, int ErrorCode)
        {
            id = DateTime.Now.ToBinary();
            ackid = AckId;
            type = Type;
            sync = Sync;
            from = From;
            to = To;
            errorcode = ErrorCode;
            TypeName = null;
        }
    }

    [ProtoContract]
    class MsgUser : MessageHeader
    {
        [ProtoMember(1, IsRequired = false)]
        public int userid { get; set; }

        [ProtoMember(2, IsRequired = false)]
        public string username { get; set; }

        [ProtoMember(3, IsRequired = false)]
        public string role { get; set; }

        [ProtoMember(4, IsRequired = false)]
        public byte[] image { get; set; }

        [ProtoMember(5, IsRequired = false)]
        public DateTime register_time { get; set; }
    }

    [ProtoContract]
    class MsgLogin : MessageHeader
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

        public MsgLogin(string text= null)
        {
            this.text = text;
        }
    }
}
