using System;

namespace Mirror
{
    public class PooledNetworkWriter : NetworkWriter, IDisposable
    {
        public void Dispose()
        {
            NetworkWriterPool.Recycle(this);
        }
    }
    public static class NetworkWriterPool
    {
        public const int MaxPoolSize = 10;

        static readonly PooledNetworkWriter[] pool = new PooledNetworkWriter[MaxPoolSize];
        static int next = -1;

        public static PooledNetworkWriter GetWriter()
        {
            if (next == -1)
            {
                return new PooledNetworkWriter();
            }

            PooledNetworkWriter writer = pool[next];
            pool[next] = null;
            next--;

            // reset cached writer length and position
            writer.SetLength(0);
            return writer;
        }

        public static void Recycle(PooledNetworkWriter writer)
        {
            if ((next + 1) < MaxPoolSize)
            {
                next++;
                pool[next] = writer;
            }
        }
    }
}
