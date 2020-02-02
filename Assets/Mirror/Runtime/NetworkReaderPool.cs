using System;
using System.Collections.Generic;

namespace Mirror
{
    public static class NetworkReaderPool
    {
        static readonly Stack<NetworkReader> pool = new Stack<NetworkReader>();

        public static NetworkReader GetReader(byte[] bytes)
        {
            if (pool.Count != 0)
            {
                NetworkReader reader = pool.Pop();
                // reset buffer
                reader.SetBuffer(bytes);
                return reader;
            }

            return new NetworkReader(bytes);
        }

        public static NetworkReader GetReader(ArraySegment<byte> segment)
        {
            if (pool.Count != 0)
            {
                NetworkReader reader = pool.Pop();
                // reset buffer
                reader.SetBuffer(segment);
                return reader;
            }

            return new NetworkReader(segment);
        }

        // NetworkReader implements IDisposable so there should only be
        // one reference to Recycle in NetworkReader's Dispose method.
        // If this shows additional references, investigate why.
        public static void Recycle(NetworkReader reader)
        {
            pool.Push(reader);
        }
    }
}
