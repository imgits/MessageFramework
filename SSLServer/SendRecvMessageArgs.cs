using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageFramework
{
    class SendRecvMessageArgs
    {
        public object           ReceivedMsg;
        public ManualResetEvent Event;
        public SendRecvMessageArgs()
        {
            ReceivedMsg = null;
            Event = new ManualResetEvent(false);
        }
    }
}
