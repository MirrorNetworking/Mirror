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
        // Mirror only uses 4 writers at a time.
        // if the user requests for 100 writers,  we just cache 10
        // and let the GC collect the other ones.
        // that way we are protected from memory leaks
        // while maintaining good performance.
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
