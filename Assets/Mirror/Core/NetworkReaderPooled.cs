// "NetworkReaderPooled" instead of "PooledNetworkReader" to group files, for
// easier IDE workflow and more elegant code.
using System;

namespace Mirror
{
    /// <summary>Pooled NetworkReader, automatically returned to pool when using 'using'</summary>
    // TODO make sealed again after removing obsolete NetworkReaderPooled!
    public class NetworkReaderPooled : NetworkReader, IDisposable
    {
        internal NetworkReaderPooled(byte[] bytes) : base(bytes) {}
        internal NetworkReaderPooled(ArraySegment<byte> segment) : base(segment) {}
        public void Dispose() => NetworkReaderPool.Return(this);
    }
}
