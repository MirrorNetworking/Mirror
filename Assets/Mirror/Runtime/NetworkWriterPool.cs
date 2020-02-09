using IDisposable = System.IDisposable;
using System.Collections.Generic;

namespace Mirror
{

    public static class NetworkWriterPool
    {
        static readonly Stack<NetworkWriter> pool = new Stack<NetworkWriter>();

        // a NetworkWriter that will put itself back in the pool
        // if disposed
        class PooledNetworkWriter : NetworkWriter, IDisposable
        {
            public void Dispose()
            {
                pool.Push(this);
            }
        }

        public static NetworkWriter GetWriter()
        {
            if (pool.Count != 0)
            {
                NetworkWriter writer = pool.Pop();
                // reset cached writer length and position
                writer.SetLength(0);
                return writer;
            }

            return new PooledNetworkWriter();
        }

        public static void Recycle(NetworkWriter writer)
        {
            pool.Push(writer);
        }
    }
}
