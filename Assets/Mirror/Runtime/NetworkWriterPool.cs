using System;

namespace Mirror
{
    /// <summary>
    /// NetworkWriter to be used with <see cref="NetworkWriterPool">NetworkWriterPool</see>
    /// </summary>
    public class PooledNetworkWriter : NetworkWriter, IDisposable
    {
        public PooledNetworkWriter(int size) : base(size) {}
        public void Dispose()
        {
            NetworkWriterPool.Recycle(this);
        }
    }

    /// <summary>
    /// Pool of NetworkWriters
    /// <para>Use this pool instead of <see cref="NetworkWriter">NetworkWriter</see> to reduce memory allocation</para>
    /// </summary>
    public static class NetworkWriterPool
    {
        // size parameter to create Writers.
        // to guarantee that all pooled writers are of same size, this can never
        // change at runtime.
        // for now let's use a size big enough for all transports.
        // TODO maybe set it to active Transport size later. but this varies
        //      between tests, so all tests would have to remember to clear the
        //      pool in Teardown.
        public const int SizeParameter = 64 * 1024;

        // reuse Pool<T>
        // we still wrap it in NetworkWriterPool.Get/Recyle so we can reset the
        // position before reusing.
        // this is also more consistent with NetworkReaderPool where we need to
        // assign the internal buffer before reusing.
        static readonly Pool<PooledNetworkWriter> pool
            = new Pool<PooledNetworkWriter>(
                () => new PooledNetworkWriter(SizeParameter)
            );

        /// <summary>
        /// Get the next writer in the pool
        /// <para>If pool is empty, creates a new Writer</para>
        /// </summary>
        public static PooledNetworkWriter GetWriter()
        {
            // grab from from pool & reset position
            PooledNetworkWriter writer = pool.Take();
            writer.Position = 0;

            // make sure that size didn't change at runtime.
            // we need to guarantee that all writers have same internal byte[].
            if (writer.Capacity != SizeParameter)
                throw new Exception($"NetworkWriterPool size parameter should not change at runtime. SizeParameter={SizeParameter} writer Capacity={writer.Capacity}");

            return writer;
        }

        /// <summary>
        /// Puts writer back into pool
        /// <para>When pool is full, the extra writer is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledNetworkWriter writer)
        {
            pool.Return(writer);
        }
    }
}
