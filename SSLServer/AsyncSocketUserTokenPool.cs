using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLServer
{
    class AsyncSocketUserTokenPool
    {
        private Stack<AsyncSocketUserToken> m_pool;
        private int m_total_tokens;
        private int m_pool_size;
        private byte[] m_receive_buffers;
        private int m_receive_buffer_size;
        public AsyncSocketUserTokenPool(int pool_size,int buf_size)
        {
            m_pool = new Stack<AsyncSocketUserToken>(pool_size);
            m_total_tokens = 0;
            m_pool_size = pool_size;
            m_receive_buffers = new byte[pool_size * buf_size];
            m_receive_buffer_size = buf_size;
        }

        public void Push(AsyncSocketUserToken item)
        {
            if (item == null)
            {
                throw new ArgumentException("Items added to a AsyncSocketUserToken cannot be null");
            }
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }

        public AsyncSocketUserToken Pop()
        {
            lock (m_pool)
            {
                if (m_pool.Count > 0) return m_pool.Pop();
                if (m_total_tokens >= m_pool_size) return null;
                m_total_tokens++;
                AsyncSocketUserToken token = new AsyncSocketUserToken(m_receive_buffers, m_receive_buffer_size * m_total_tokens++, m_receive_buffer_size);
                return token;
            }
        }

        public int Count
        {
            get { return m_pool.Count; }
        }
    }
}

