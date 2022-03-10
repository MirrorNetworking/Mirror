// "NetworkWriterPooled" instead of "PooledNetworkWriter" to group files, for
// easier IDE workflow and more elegant code.
using System;

namespace Mirror
{
    // DEPRECATED 2022-03-10
    [Obsolete("PooledNetworkWriter was renamed to NetworkWriterPooled. It's cleaner & slightly easier to use.")]
    public sealed class PooledNetworkWriter : NetworkWriterPooled {}

    /// <summary>Pooled NetworkWriter, automatically returned to pool when using 'using'</summary>
    // TODO make sealed again after removing obsolete NetworkWriterPooled!
    public class NetworkWriterPooled : NetworkWriter, IDisposable
    {
        public void Dispose() => NetworkWriterPool.Return(this);
    }
}
