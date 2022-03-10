// API consistent with Microsoft's ObjectPool<T>.
using System;
using System.Runtime.CompilerServices;

namespace Mirror
{
    /// <summary>Pool of NetworkWriters to avoid allocations.</summary>
    public static class NetworkWriterPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkWriterPool.Get/Recycle so we can reset the
        // position before reusing.
        // this is also more consistent with NetworkReaderPool where we need to
        // assign the internal buffer before reusing.
        static readonly Pool<NetworkWriterPooled> Pool = new Pool<NetworkWriterPooled>(
            () => new NetworkWriterPooled(),
            // initial capacity to avoid allocations in the first few frames
            // 1000 * 1200 bytes = around 1 MB.
            1000
        );

        // DEPRECATED 2022-03-10
        [Obsolete("GetWriter() was renamed to Get()")]
        public static NetworkWriterPooled GetWriter() => Get();

        /// <summary>Get a writer from the pool. Creates new one if pool is empty.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkWriterPooled Get()
        {
            // grab from pool & reset position
            NetworkWriterPooled writer = Pool.Get();
            writer.Reset();
            return writer;
        }

        // DEPRECATED 2022-03-10
        [Obsolete("Recycle() was renamed to Return()")]
        public static void Recycle(NetworkWriterPooled writer) => Return(writer);

        /// <summary>Return a writer to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(NetworkWriterPooled writer)
        {
            Pool.Return(writer);
        }
    }
}
