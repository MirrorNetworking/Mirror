using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// NetworkReader to be used with <see cref="NetworkReaderPool">NetworkReaderPool</see>
    /// </summary>
    public class PooledNetworkReader : NetworkReader, IDisposable
    {
        internal PooledNetworkReader(byte[] bytes) : base(bytes) { }

        internal PooledNetworkReader(ArraySegment<byte> segment) : base(segment) { }

        public void Dispose()
        {
            NetworkReaderPool.Recycle(this);
        }
    }

    /// <summary>
    /// Pool of NetworkReaders
    /// <para>Use this pool instead of <see cref="NetworkReader">NetworkReader</see> to reduce memory allocation</para>
    /// <para>Use <see cref="Capacity">Capacity</see> to change size of pool</para>
    /// </summary>
    public static class NetworkReaderPool
    {
        /// <summary>
        /// Size of the pool
        /// <para>If pool is too small getting readers will causes memory allocation</para>
        /// <para>Default value: 100</para>
        /// </summary>
        public static int Capacity
        {
            get => pool.Length;
            set
            {
                // resize the array
                Array.Resize(ref pool, value);

                // if capacity is smaller than before, then we need to adjust
                // 'next' so it doesn't point to an index out of range
                // -> if we set '0' then next = min(_, 0-1) => -1
                // -> if we set '2' then next = min(_, 2-1) =>  1
                next = Mathf.Min(next, pool.Length - 1);
            }
        }

        /// <summary>
        /// Mirror usually only uses up to 4 readers in nested usings,
        /// 100 is a good margin for edge cases when users need a lot readers at
        /// the same time.
        ///
        /// <para>keep in mind, most entries of the pool will be null in most cases</para>
        /// </summary>
        ///
        /// Note: we use an Array instead of a Stack because it's significantly
        ///       faster: https://github.com/vis2k/Mirror/issues/1614
        static PooledNetworkReader[] pool = new PooledNetworkReader[100];

        static int next = -1;

        /// <summary>
        /// Get the next reader in the pool
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        public static PooledNetworkReader GetReader(byte[] bytes)
        {
            if (next == -1)
            {
                return new PooledNetworkReader(bytes);
            }

            PooledNetworkReader reader = pool[next];
            pool[next] = null;
            next--;

            // reset buffer
            SetBuffer(reader, bytes);
            return reader;
        }

        /// <summary>
        /// Get the next reader in the pool
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        public static PooledNetworkReader GetReader(ArraySegment<byte> segment)
        {
            if (next == -1)
            {
                return new PooledNetworkReader(segment);
            }

            PooledNetworkReader reader = pool[next];
            pool[next] = null;
            next--;

            // reset buffer
            SetBuffer(reader, segment);
            return reader;
        }

        /// <summary>
        /// Puts reader back into pool
        /// <para>When pool is full, the extra reader is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledNetworkReader reader)
        {
            if (next < pool.Length - 1)
            {
                next++;
                pool[next] = reader;
            }
            else
            {
                if (LogFilter.Debug) { Debug.LogWarning("NetworkReaderPool.Recycle, Pool was full leaving extra reader for GC"); }
            }
        }

        // SetBuffer methods mirror constructor for ReaderPool
        static void SetBuffer(NetworkReader reader, byte[] bytes)
        {
            reader.buffer = new ArraySegment<byte>(bytes);
            reader.Position = 0;
        }

        static void SetBuffer(NetworkReader reader, ArraySegment<byte> segment)
        {
            reader.buffer = segment;
            reader.Position = 0;
        }
    }
}
