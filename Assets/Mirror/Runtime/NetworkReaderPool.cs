using System;
using System.Runtime.CompilerServices;

namespace Mirror
{
    [Obsolete("PooledNetworkReader was renamed to NetworkReaderPooled. It's cleaner & slightly easier to use.")]
    public sealed class PooledNetworkReader : NetworkReaderPooled
    {
        internal PooledNetworkReader(byte[] bytes) : base(bytes) {}
        internal PooledNetworkReader(ArraySegment<byte> segment) : base(segment) {}
    }

    /// <summary>Pooled NetworkReader, automatically returned to pool when using 'using'</summary>
    // TODO make sealed again after removing obsolete NetworkWriterPooled!
    public class NetworkReaderPooled : NetworkReader, IDisposable
    {
        internal NetworkReaderPooled(byte[] bytes) : base(bytes) {}
        internal NetworkReaderPooled(ArraySegment<byte> segment) : base(segment) {}
        public void Dispose() => NetworkReaderPool.Return(this);
    }

    /// <summary>Pool of NetworkReaders to avoid allocations.</summary>
    public static class NetworkReaderPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkReaderPool.Get/Recyle so we can reset the
        // position and array before reusing.
        static readonly Pool<NetworkReaderPooled> Pool = new Pool<NetworkReaderPooled>(
            // byte[] will be assigned in GetReader
            () => new NetworkReaderPooled(new byte[]{}),
            // initial capacity to avoid allocations in the first few frames
            1000
        );

        // DEPRECATED 2022-03-10
        [Obsolete("GetReader() was renamed to Get()")]
        public static NetworkReaderPooled GetReader(byte[] bytes) => Get(bytes);

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkReaderPooled Get(byte[] bytes)
        {
            // grab from pool & set buffer
            NetworkReaderPooled reader = Pool.Get();
            reader.SetBuffer(bytes);
            return reader;
        }

        // DEPRECATED 2022-03-10
        [Obsolete("GetReader() was renamed to Get()")]
        public static NetworkReaderPooled GetReader(ArraySegment<byte> segment) => Get(segment);

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkReaderPooled Get(ArraySegment<byte> segment)
        {
            // grab from pool & set buffer
            NetworkReaderPooled reader = Pool.Get();
            reader.SetBuffer(segment);
            return reader;
        }

        // DEPRECATED 2022-03-10
        [Obsolete("Recycle() was renamed to Return()")]
        public static void Recycle(NetworkReaderPooled reader) => Return(reader);

        /// <summary>Returns a reader to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(NetworkReaderPooled reader)
        {
            Pool.Return(reader);
        }
    }
}
