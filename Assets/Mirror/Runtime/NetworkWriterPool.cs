using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class NetworkWriterPool
    {
        // reuse all writers, saves tons of memory allocations in hotpath
        static readonly Stack<NetworkWriter> writerPool = new Stack<NetworkWriter>();

        public static NetworkWriter GetPooledWriter()
        {
            if (writerPool.Count != 0)
            {
                NetworkWriter writer = writerPool.Pop();
                writer.pooled = false;
                // reset cached writer length and position
                writer.SetLength(0);
                return writer;
            }

            return new NetworkWriter(true);
        }

        public static void Recycle(NetworkWriter writer)
        {
            if (writer == null)
            {
                Debug.LogWarning("Recycling null writers is not allowed, please check your code!");
                return;
            }
            if (writer.reusable && !writer.pooled)
            {
                writer.pooled = true;
                writerPool.Push(writer);
            }
        }
    }
}
