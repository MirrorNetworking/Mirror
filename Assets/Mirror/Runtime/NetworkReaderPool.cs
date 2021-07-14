using System;

namespace Mirror
{
    /// <summary>Pooled NetworkReader, automatically returned to pool when using 'using'</summary>
    public sealed class PooledNetworkReader : NetworkReader, IDisposable
    {
        internal PooledNetworkReader(byte[] bytes) : base(bytes) {}
        internal PooledNetworkReader(ArraySegment<byte> segment) : base(segment) {}
        public void Dispose() => NetworkReaderPool.Recycle(this);
    }

    /// <summary>Pool of NetworkReaders to avoid allocations.</summary>
    public static class NetworkReaderPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkReaderPool.Get/Recyle so we can reset the
        // position and array before reusing.
        static readonly Pool<PooledNetworkReader> Pool = new Pool<PooledNetworkReader>(
            // byte[] will be assigned in GetReader
            () => new PooledNetworkReader(new byte[]{}),
            // initial capacity to avoid allocations in the first few frames
            1000
        );

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        public static PooledNetworkReader GetReader(byte[] bytes)
        {
            // grab from pool & set buffer
            PooledNetworkReader reader = Pool.Take();
            reader.SetBuffer(bytes);
            return reader;
        }

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        public static PooledNetworkReader GetReader(ArraySegment<byte> segment)
        {
            // grab from pool & set buffer
            PooledNetworkReader reader = Pool.Take();
            reader.SetBuffer(segment);
            return reader;
        }

        /// <summary>Returns a reader to the pool.</summary>
        public static void Recycle(PooledNetworkReader reader)
        {
            Pool.Return(reader);
        }
    }
}
