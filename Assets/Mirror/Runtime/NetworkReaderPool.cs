using System;
using System.Runtime.CompilerServices;

namespace Mirror
{
    /// <summary>Pooled NetworkReader, automatically returned to pool when using 'using'</summary>
    public sealed class PooledNetworkReader : NetworkReader, IDisposable
    {
        internal PooledNetworkReader(byte[] bytes) : base(bytes) {}
        internal PooledNetworkReader(ArraySegment<byte> segment) : base(segment) {}
        public void Dispose() => NetworkReaderPool.Return(this);
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

        // DEPRECATED 2022-03-10
        [Obsolete("GetReader() was renamed to Get()")]
        public static PooledNetworkReader GetReader(byte[] bytes) => Get(bytes);

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledNetworkReader Get(byte[] bytes)
        {
            // grab from pool & set buffer
            PooledNetworkReader reader = Pool.Get();
            reader.SetBuffer(bytes);
            return reader;
        }

        // DEPRECATED 2022-03-10
        [Obsolete("GetReader() was renamed to Get()")]
        public static PooledNetworkReader GetReader(ArraySegment<byte> segment) => Get(segment);

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledNetworkReader Get(ArraySegment<byte> segment)
        {
            // grab from pool & set buffer
            PooledNetworkReader reader = Pool.Get();
            reader.SetBuffer(segment);
            return reader;
        }

        // DEPRECATED 2022-03-10
        [Obsolete("Recycle() was renamed to Return()")]
        public static void Recycle(PooledNetworkReader reader) => Return(reader);

        /// <summary>Returns a reader to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(PooledNetworkReader reader)
        {
            Pool.Return(reader);
        }
    }
}
