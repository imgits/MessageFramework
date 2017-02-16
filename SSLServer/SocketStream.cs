using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SSLServer
{
 
    class SocketStream : Stream
    {
        readonly Object _syncRoot = new object();
        ByteStream RecvStream = new ByteStream();
        Queue<ByteBuffer> SendPacketQueue = new Queue<ByteBuffer>();
        Queue<ByteBuffer> RecvPacketQueue = new Queue<ByteBuffer>();
        volatile Boolean _Closed;

        public SocketStream()
        {
            _Closed = false;
        }

        public override Int32 ReadByte()
        {
            lock (_syncRoot)
            {
                if (!WaitForRecvData()) return -1;
                Int32 ReadByte = RecvStream.ReadByte();
                return ReadByte;
            }
        }

        /// <summary>
        /// Socket.Recv()<--SocketStream.Read()<--SslStream.Read()<--App.Recv()
        /// 获取Socket已接收的数据
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            lock (_syncRoot)
            {
                if (!WaitForRecvData()) return -1;
                Int32 ReadBytes = RecvStream.Read(buffer, offset, count);
                return ReadBytes;
            }
        }

        public override void Close()
        {
            base.Close();

            if (_Closed) return;

            lock (_syncRoot)
            {
                _Closed = true;
                RecvStream.Clear();
                Monitor.PulseAll(_syncRoot);
            }
        }

        /// <summary>
        /// Socket.Send()<--SocketStream.Write()<--SslStream.Write()<--App.Sned()
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            
        }

        public override void WriteByte(Byte value)
        {
        }

        public override void Flush()
        {
        }

        public void WriteRecvData(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (_Closed)  return;

            lock (_syncRoot)
            {
                RecvStream.Write(buffer, offset, count);
            }
        }

        private bool WaitForRecvData()
        {
            while (!_Closed)
            {
                try
                {
                    while(RecvStream.Length <=0) Monitor.Wait(_syncRoot);
                    return true;
                }
                catch (ThreadInterruptedException e)
                {
                    //throw new IOException("Interrupted while waiting for more data", e);
                }
            }
            return false;
        }

 
        public override Boolean CanRead
        {
            get { return true; }
        }

        public override Boolean CanSeek
        {
            get { return false; }
        }

        public override Boolean CanWrite
        {
            get { return true; }
        }

        public override Int64 Length
        {
            get { throw new NotSupportedException(); }
        }

        public override Int64 Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override Int64 Seek(Int64 offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(Int64 value)
        {
            throw new NotSupportedException();
        }

    }
}
