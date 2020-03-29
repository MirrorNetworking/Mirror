using System;

namespace Mirror
{
    /// <summary>
    /// NetworkWriter to be used with <see cref="NetworkWriterPool">NetworkWriterPool</see>
    /// </summary>
    public class PooledNetworkWriter : NetworkWriter, IDisposable
    {
        public void Dispose()
        {
            NetworkWriterPool.Recycle(this);
        }
    }

    /// <summary>
    /// Pool of NetworkWriters
    /// <para>Use this pool instead of <see cref="NetworkWriter">NetworkWriter</see> to reduce memory allocation</para>
    /// <para>Use <see cref="ResizePool">ResizePool</see> to change size of pool</para>
    /// </summary>
    public static class NetworkWriterPool
    {
        /// <summary>
        /// Mirror only uses 4 writers at a time,
        /// 100 is a good margin for edge cases when
        /// users need a lot writers at the same time.
        ///
        /// <para>keep in mind, most entries of the pool will be null in most cases</para>
        /// </summary>
        const int PoolStartSize = 100;

        /// <summary>
        /// Current Size of Pool
        /// </summary>
        public static int PoolSize => pool.Length;

        /// <summary>
        /// Change size of pool
        /// <para>If pool is too small getting writers will causes memory allocation</para>
        /// </summary>
        /// <param name="newSize"></param>
        public static void ResizePool(int newSize)
        {
            Array.Resize(ref pool, newSize);
        }

        static PooledNetworkWriter[] pool = new PooledNetworkWriter[PoolStartSize];

        static int next = -1;

        /// <summary>
        /// Get the next writer in the pool
        /// <para>If pool is empty, creates a new Writer</para>
        /// </summary>
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

        /// <summary>
        /// Puts writer back into pool
        /// <para>If pool is full, the writer is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledNetworkWriter writer)
        {
            if ((next + 1) < pool.Length)
            {
                next++;
                pool[next] = writer;
            }
        }
    }
}
