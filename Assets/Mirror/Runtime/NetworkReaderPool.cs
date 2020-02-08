using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkReaderPool : NetworkReader, IDisposable
    {
        private NetworkReaderPool(byte[] bytes) : base(bytes) { }

        private NetworkReaderPool(ArraySegment<byte> segment) : base(segment) { }

        static readonly Stack<NetworkReaderPool> pool = new Stack<NetworkReaderPool>();

        public static NetworkReaderPool GetReader(byte[] bytes)
        {
            if (pool.Count != 0)
            {
                NetworkReaderPool reader = pool.Pop();
                // reset buffer
                SetBuffer(reader, bytes);
                return reader;
            }

            return new NetworkReaderPool(bytes);
        }

        public static NetworkReaderPool GetReader(ArraySegment<byte> segment)
        {
            if (pool.Count != 0)
            {
                NetworkReaderPool reader = pool.Pop();
                // reset buffer
                SetBuffer(reader, segment);
                return reader;
            }

            return new NetworkReaderPool(segment);
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

        public static void Recycle(NetworkReaderPool reader)
        {
            pool.Push(reader);
        }

        public void Dispose()
        {
            Recycle(this);
        }
    }
}
