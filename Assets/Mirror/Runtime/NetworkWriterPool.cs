using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkWriterPool : NetworkWriter, IDisposable
    {
        NetworkWriterPool() { }

        static readonly Stack<NetworkWriterPool> pool = new Stack<NetworkWriterPool>();

        public static NetworkWriterPool GetWriter()
        {
            if (pool.Count != 0)
            {
                NetworkWriterPool writer = pool.Pop();
                // reset cached writer length and position
                writer.SetLength(0);
                return writer;
            }

            return new NetworkWriterPool();
        }

        public static void Recycle(NetworkWriterPool writer)
        {
            pool.Push(writer);
        }

        public void Dispose()
        {
            Recycle(this);
        }
    }
}
