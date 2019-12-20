using System.IO;

namespace Ninja.WebSockets
{
    public interface IBufferPool
    {
        /// <summary>
        /// Gets a MemoryStream built from a buffer plucked from a thread safe pool
        /// The pool grows automatically.
        /// Closing the memory stream clears the buffer and returns it to the pool
        /// </summary>
        MemoryStream GetBuffer();
    }
}
