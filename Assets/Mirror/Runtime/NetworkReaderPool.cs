using System;
using System.Collections.Generic;

namespace Mirror
{
    public static class NetworkReaderPool
    {
        static readonly Stack<NetworkReader> pool = new Stack<NetworkReader>();

        public static NetworkReader GetReader(byte[] bytes)
        {
            UnityEngine.Debug.Log($"NetworkReaderPool:GetReader byte[] {pool.Count}");

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
            UnityEngine.Debug.Log($"NetworkReaderPool:GetReader ArraySegment {pool.Count}");

            if (pool.Count != 0)
            {
                NetworkReader reader = pool.Pop();
                // reset buffer
                reader.SetBuffer(segment);
                return reader;
            }

            return new NetworkReader(segment);
        }

        public static void Recycle(NetworkReader reader)
        {
            UnityEngine.Debug.Log($"NetworkReaderPool:Recycle {pool.Count}");

            pool.Push(reader);
        }
    }
}
