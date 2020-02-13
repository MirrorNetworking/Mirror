using System;
using System.Collections.Generic;

namespace Mirror
{
    // a NetworkReader that will recycle itself when disposed
    public class PooledNetworkReader : NetworkReader, IDisposable
    {
        internal PooledNetworkReader(byte[] bytes) : base(bytes) { }

        internal PooledNetworkReader(ArraySegment<byte> segment) : base(segment) { }

        public void Dispose()
        {
            NetworkReaderPool.Recycle(this);
        }
    }

    public static class NetworkReaderPool
    {
        static readonly Stack<PooledNetworkReader> pool = new Stack<PooledNetworkReader>();

        public static PooledNetworkReader GetReader(byte[] bytes)
        {
            if (pool.Count != 0)
            {
                PooledNetworkReader reader = pool.Pop();
                // reset buffer
                SetBuffer(reader, bytes);
                return reader;
            }

            return new PooledNetworkReader(bytes);
        }

        public static PooledNetworkReader GetReader(ArraySegment<byte> segment)
        {
            if (pool.Count != 0)
            {
                PooledNetworkReader reader = pool.Pop();
                // reset buffer
                SetBuffer(reader, segment);
                return reader;
            }

            return new PooledNetworkReader(segment);
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

        public static void Recycle(PooledNetworkReader reader)
        {
            pool.Push(reader);
        }
    }
}
