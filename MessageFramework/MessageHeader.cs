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
        public long     id { get; set; }
        public long     ackid { get; set; }
        public string   from { get; set; }
        public string   to { get; set; }
        public long     result { get; set; }

        public MessageHeader()
            : this(0, null, null, 0)
        {
        }

        public MessageHeader(MessageHeader msghdr)
            : this(msghdr.ackid, msghdr.from, msghdr.to, msghdr.result)
        {
        }

        public MessageHeader(string To)
            : this(0, null, To, 0)
        {
        }

        public MessageHeader(string From, string To)
            : this(0,From, To, 0)
        {
        }

        public MessageHeader(long AckId, string From, string To, long Result)
        {
            InitHeader(AckId, From, To, Result);
        }

        public void InitHeader(MessageHeader msghdr)
        {
            InitHeader(msghdr.ackid, msghdr.from, msghdr.to, msghdr.result);
        }

        public void InitHeader(string To)
        {
            InitHeader(0, null, To, 0);
        }

        public void InitHeader(string From, string To)
        {
            InitHeader(0, From, To, 0);
        }

        public void InitHeader(long AckId, string From, string To, long Result)
        {
            id = DateTime.Now.ToBinary();
            ackid = AckId;
            from = From;
            to = To;
            result = Result;
        }

    }
}
