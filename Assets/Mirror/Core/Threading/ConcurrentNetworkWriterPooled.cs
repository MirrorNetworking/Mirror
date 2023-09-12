using System;

namespace Mirror
{
    /// <summary>Pooled (not threadsafe) NetworkWriter used from Concurrent pool (thread safe). Automatically returned to concurrent pool when using 'using'</summary>
    // TODO make sealed again after removing obsolete NetworkWriterPooled!
    public class ConcurrentNetworkWriterPooled : NetworkWriter, IDisposable
    {
        public void Dispose() => ConcurrentNetworkWriterPool.Return(this);
    }
}
