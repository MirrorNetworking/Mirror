using System;

namespace Mirror
{
    /// <summary>
    /// NetworkWriter to be used with <see cref="NetworkWriterPool">NetworkWriterPool</see>
    /// </summary>
    public sealed class PooledNetworkWriter : NetworkWriter, IDisposable
    {
        public void Dispose() => NetworkWriterPool.Recycle(this);
    }

    /// <summary>
    /// Pool of NetworkWriters
    /// <para>Use this pool instead of <see cref="NetworkWriter">NetworkWriter</see> to reduce memory allocation</para>
    /// </summary>
    public static class NetworkWriterPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkWriterPool.Get/Recycle so we can reset the
        // position before reusing.
        // this is also more consistent with NetworkReaderPool where we need to
        // assign the internal buffer before reusing.
        static readonly Pool<PooledNetworkWriter> Pool = new Pool<PooledNetworkWriter>(
            () => new PooledNetworkWriter()
        );

        /// <summary>
        /// Get the next writer in the pool
        /// <para>If pool is empty, creates a new Writer</para>
        /// </summary>
        public static PooledNetworkWriter GetWriter()
        {
            // grab from pool & reset position
            PooledNetworkWriter writer = Pool.Take();
            writer.Reset();
            return writer;
        }

        /// <summary>
        /// Puts writer back into pool
        /// <para>When pool is full, the extra writer is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledNetworkWriter writer)
        {
            Pool.Return(writer);
        }
    }
}
