using System;

namespace Mirror
{
    /// <summary>
    /// NetworkReader to be used with <see cref="NetworkReaderPool">NetworkReaderPool</see>
    /// </summary>
    public sealed class PooledNetworkReader : NetworkReader, IDisposable
    {
        internal PooledNetworkReader(byte[] bytes) : base(bytes) {}
        internal PooledNetworkReader(ArraySegment<byte> segment) : base(segment) {}

        public void Dispose()
        {
            NetworkReaderPool.Recycle(this);
        }
    }

    /// <summary>
    /// Pool of NetworkReaders
    /// <para>Use this pool instead of <see cref="NetworkReader">NetworkReader</see> to reduce memory allocation</para>
    /// </summary>
    public static class NetworkReaderPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkReaderPool.Get/Recyle so we can reset the
        // position and array before reusing.
        static readonly Pool<PooledNetworkReader> pool = new Pool<PooledNetworkReader>(
            // byte[] will be assigned in GetReader
            () => new PooledNetworkReader(new byte[]{})
        );

        /// <summary>
        /// Get the next reader in the pool
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        public static PooledNetworkReader GetReader(byte[] bytes)
        {
            // grab from pool & set buffer
            PooledNetworkReader reader = pool.Take();
            reader.buffer = new ArraySegment<byte>(bytes);
            reader.Position = 0;
            return reader;
        }

        /// <summary>
        /// Get the next reader in the pool
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        public static PooledNetworkReader GetReader(ArraySegment<byte> segment)
        {
            // grab from pool & set buffer
            PooledNetworkReader reader = pool.Take();
            reader.buffer = segment;
            reader.Position = 0;
            return reader;
        }

        /// <summary>
        /// Puts reader back into pool
        /// <para>When pool is full, the extra reader is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledNetworkReader reader)
        {
            pool.Return(reader);
        }
    }
}
