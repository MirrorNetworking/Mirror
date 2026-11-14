using System;

namespace Mirror
{
    /// <summary>Pooled NetworkWriter, automatically returned to pool when using 'using'</summary>
    public sealed class NetworkWriterPooled : NetworkWriter, IDisposable
    {
        public void Dispose() => NetworkWriterPool.Return(this);
    }
}
