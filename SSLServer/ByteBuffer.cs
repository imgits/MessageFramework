using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MessageFramework
{
    class ByteBuffer
    {
        private byte[] _Buffer;
        private Int32 _Offset;
        private Int32 _Count;
        private Int32 _Size;

        public ByteBuffer(int size)
        {
            _Buffer = new byte[size];
            _Offset = 0;
            _Count = 0;
            _Size = size;
        }

        public ByteBuffer(byte[] buffer, int offset, int count)
        {
            _Buffer = new byte[count];
            _Offset = 0;
            _Count = count;
            _Size = count;
            Buffer.BlockCopy(buffer, offset, _Buffer, 0, count);
        }

        public Int32 Count { get { return _Count; } }

        public bool IsEmpty { get { return _Count == 0; } }

        public void Clear()
        {
            lock (_Buffer)
            {
                _Offset = 0;
                _Count = 0;
            }
        }

        public Int32 ReadByte()
        {
            lock (_Buffer)
            {
                if (_Count > 0)
                {
                    _Count--;
                    return _Buffer[_Offset++];
                }
                else
                {
                    return -1;
                }
            }
        }

        public Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            lock (_Buffer)
            {
                if (IsEmpty) return 0;
                Int32 ReadBytes = _Count < count ? _Count : count;
                System.Buffer.BlockCopy(_Buffer, _Offset, buffer, offset, ReadBytes);
                _Offset += ReadBytes;
                _Count -= ReadBytes;
                return ReadBytes;
            }
        }

        public Int32 PeekByte()
        {
            lock (_Buffer)
            {
                if (_Count > 0)
                {
                    return _Buffer[_Offset];
                }
                else
                {
                    return -1;
                }
            }
        }

        public Int32 Peek(Byte[] buffer, Int32 offset, Int32 count)
        {
            lock (_Buffer)
            {
                if (IsEmpty) return 0;
                Int32 PeekBytes = _Count < count ? _Count : count;
                System.Buffer.BlockCopy(_Buffer, _Offset, buffer, offset, PeekBytes);
                return PeekBytes;
            }
        }

        public Int32 Skip(int count)
        {
            lock (_Buffer)
            {
                if (IsEmpty) return 0;
                Int32 SkipBytes = _Count < count ? _Count : count;
                _Offset += SkipBytes;
                _Count -= SkipBytes;
                return SkipBytes;
            }
        }

        public bool WriteByte(Byte value)
        {
            lock (_Buffer)
            {
                Int32 FreeBytes = _Size - _Offset - _Count;
                if (FreeBytes < 1)
                {//尾部空间不够
                    if (_Offset > 0)
                    {//将现有数据移至缓冲区头部
                        System.Buffer.BlockCopy(_Buffer, _Offset, _Buffer, 0, _Count);
                        FreeBytes += _Offset;
                        _Offset = 0;
                    }
                }
                if (FreeBytes >= 1)
                {
                    _Buffer[_Count++] = value;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public Int32 Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            lock (_Buffer)
            {
                Int32 FreeBytes = _Size - _Offset - _Count;
                if (FreeBytes < count)
                {//尾部空间不够
                    if (_Offset > 0)
                    {//将现有数据移至缓冲区头部
                        System.Buffer.BlockCopy(_Buffer, _Offset, _Buffer, 0, _Count);
                        FreeBytes += _Offset;
                        _Offset = 0;
                    }
                }
                if (FreeBytes >= count)
                {
                    System.Buffer.BlockCopy(buffer, offset, _Buffer, _Offset, count);
                    _Count += count;
                    return count;
                }
                else 
                {//数据移动后还不够，则将缓冲区写满为止
                    System.Buffer.BlockCopy(buffer, offset, _Buffer, _Count, FreeBytes);
                    _Count += FreeBytes;
                    return FreeBytes;
                }
            }
        }
    }
}
