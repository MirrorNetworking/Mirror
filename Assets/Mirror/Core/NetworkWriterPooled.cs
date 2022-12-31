// "NetworkWriterPooled" instead of "PooledNetworkWriter" to group files, for
// easier IDE workflow and more elegant code.
using System;

namespace Mirror
{
    /// <summary>Pooled NetworkWriter, automatically returned to pool when using 'using'</summary>
    // TODO make sealed again after removing obsolete NetworkWriterPooled!
    public class NetworkWriterPooled : NetworkWriter, IDisposable
    {
        public void Dispose() => NetworkWriterPool.Return(this);
    }
}
