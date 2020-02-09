using System;
using System.Collections.Generic;

namespace Mirror
{
    // a NetworkWriter that will recycle itself when disposed
    public class PooledNetworkWriter : NetworkWriter, IDisposable
    {
        public void Dispose()
        {
            NetworkWriterPool.Recycle(this);
        }
    }

    public static class NetworkWriterPool
    {
        static readonly Stack<PooledNetworkWriter> pool = new Stack<PooledNetworkWriter>();

        public static PooledNetworkWriter GetWriter()
        {
            if (pool.Count != 0)
            {
                PooledNetworkWriter writer = pool.Pop();
                // reset cached writer length and position
                writer.SetLength(0);
                return writer;
            }

            return new PooledNetworkWriter();
        }

        public static void Recycle(PooledNetworkWriter writer)
        {
            pool.Push(writer);
        }
    }
}
