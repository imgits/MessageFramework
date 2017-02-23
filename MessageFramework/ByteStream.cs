using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageFramework
{
    public class ByteStream
    {
        Queue<ByteBuffer> _StreamBuffers = new Queue<ByteBuffer>();
        readonly int _BufferSize = 4096;
        int _Length;
        public ByteStream(int buffer_size = 4096)
        {
            _BufferSize = buffer_size;
            _Length = 0;
        }

        private ByteBuffer AddBuffer()
        {
            ByteBuffer buf = new ByteBuffer(_BufferSize);
            _StreamBuffers.Enqueue(buf);
            return buf;
        }

        public int Length { get { return _Length;} }

        public int ReadByte()
        {
            lock (_StreamBuffers)
            {
                int value = -1;
                do
                {
                    if (_StreamBuffers.Count == 0) break;
                    ByteBuffer buf = _StreamBuffers.Peek();
                    if (buf == null) break;
                    value = buf.ReadByte();
                    if (buf.IsEmpty) _StreamBuffers.Dequeue();
                } while (value == -1);
                if (value != -1) _Length--;
                return value;
            }
        }

        public int Read( byte[] buffer,   int offset,  int count )
        {
            lock (_StreamBuffers)
            {
                int ReadBytes = 0;
                while (count > 0)
                {
                    if (_StreamBuffers.Count == 0) break;
                    ByteBuffer buf = _StreamBuffers.Peek();
                    if (buf == null) break;
                    int bytes = buf.Read(buffer, offset, count);
                    if (bytes > 0)
                    {
                        ReadBytes += bytes;
                        offset += bytes;
                        count -= bytes;
                    }
                    if (buf.IsEmpty) _StreamBuffers.Dequeue();
                }
                _Length-= ReadBytes;
                return ReadBytes;
            }
        }

        public int PeekByte()
        {
            lock (_StreamBuffers)
            {
                int value = -1;
                do
                {
                    if (_StreamBuffers.Count == 0) break;
                    ByteBuffer buf = _StreamBuffers.Peek();
                    if (buf == null) break;
                    value = buf.PeekByte();
                } while (value == -1);
                return value;
            }
        }

        public int Peek(byte[] buffer, int offset, int count)
        {
            lock (_StreamBuffers)
            {
                int PeekBytes = 0;
                foreach(ByteBuffer buf in _StreamBuffers)
                {
                    int bytes = buf.Peek(buffer, offset, count);
                    PeekBytes += bytes;
                    offset += bytes;
                    count -= bytes;
                    if (count <= 0) break;
                }
                return PeekBytes;
            }
        }

        public Int32 Skip(int count)
        {
            lock (_StreamBuffers)
            {
                int SkipBytes = 0;
                while (count > 0)
                {
                    if (_StreamBuffers.Count == 0) break;
                    ByteBuffer buf = _StreamBuffers.Peek();
                    if (buf == null) break;
                    int bytes = buf.Skip(count);
                    SkipBytes += bytes;
                    count -= bytes;
                    if (buf.IsEmpty) _StreamBuffers.Dequeue();
                }
                _Length -= SkipBytes;
                return SkipBytes;
            }
        }

        public void WriteByte(byte value)
        {
            lock (_StreamBuffers)
            {
                ByteBuffer buf = null;
                if (_StreamBuffers.Count > 0) buf = _StreamBuffers.Last();
                if (buf == null || !buf.WriteByte(value))
                {
                    buf = AddBuffer();
                    buf.WriteByte(value);
                }
                _Length++;
            }
        }

        public void Write(byte[] buffer,  int offset, int count)
        {
            lock (_StreamBuffers)
            {
                ByteBuffer buf = null;
                if (_StreamBuffers.Count > 0) buf = _StreamBuffers.Last();
                if (buf == null) buf = AddBuffer();
                int BytesWritten = count;
                while (count > 0)
                {
                    int bytes = buf.Write(buffer, offset, count);
                    offset += bytes;
                    count -= bytes;
                    if (count >0) buf = AddBuffer();
                }
                _Length += BytesWritten;
            }
        }

        public bool DataAvailable
        {
            get
            {
                lock (_StreamBuffers)
                {
                    return (_Length > 0);
                }
            }
        }

        public void Clear()
        {
            lock (_StreamBuffers)
            {
                while (_StreamBuffers.Count > 0) _StreamBuffers.Dequeue();
                _Length = 0;
            }
        }
    }
}
