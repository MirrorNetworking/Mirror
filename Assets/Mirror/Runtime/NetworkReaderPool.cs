using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkReaderPool : NetworkReader, IDisposable
    {
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

            return new NetworkReader(bytes) as NetworkReaderPool;
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

            return new NetworkReader(segment) as NetworkReaderPool;
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

        // NetworkReader implements IDisposable so there should only be
        // one reference to Recycle in NetworkReader's Dispose method.
        // If this shows additional references, investigate why.
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
