using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ninja.WebSockets
{
    /// <summary>
    /// This buffer pool is instance thread safe
    /// Use GetBuffer to get a MemoryStream (with a publically accessible buffer)
    /// Calling Close on this MemoryStream will clear its internal buffer and return the buffer to the pool for reuse
    /// MemoryStreams can grow larger than the DEFAULT_BUFFER_SIZE (or whatever you passed in)
    /// and the underlying buffers will be returned to the pool at their larger sizes
    /// </summary>
    public class BufferPool : IBufferPool
    {
        const int DEFAULT_BUFFER_SIZE = 16384;
        readonly ConcurrentStack<byte[]> _bufferPoolStack;
        readonly int _bufferSize;

        public BufferPool() : this(DEFAULT_BUFFER_SIZE)
        {
        }

        public BufferPool(int bufferSize)
        {
            _bufferSize = bufferSize;
            _bufferPoolStack = new ConcurrentStack<byte[]>();
        }

        /// <summary>
        /// This memory stream is not instance thread safe (not to be confused with the BufferPool which is instance thread safe)
        /// </summary>
        protected class PublicBufferMemoryStream : MemoryStream
        {
            readonly BufferPool _bufferPoolInternal;
            byte[] _buffer;
            MemoryStream _ms;

            public PublicBufferMemoryStream(byte[] buffer, BufferPool bufferPool) : base(new byte[0])
            {
                _bufferPoolInternal = bufferPool;
                _buffer = buffer;
                _ms = new MemoryStream(buffer, 0, buffer.Length, true, true);
            }

            public override long Length => base.Length;

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return _ms.BeginRead(buffer, offset, count, callback, state);
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return _ms.BeginWrite(buffer, offset, count, callback, state);
            }

            public override bool CanRead => _ms.CanRead;
            public override bool CanSeek => _ms.CanSeek;
            public override bool CanTimeout => _ms.CanTimeout;
            public override bool CanWrite => _ms.CanWrite;
            public override int Capacity
            {
                get { return _ms.Capacity; }
                set { _ms.Capacity = value; }
            }

            public override void Close()
            {
                // clear the buffer - we only need to clear up to the number of bytes we have already written
                Array.Clear(_buffer, 0, (int)_ms.Position);

                _ms.Close();

                // return the buffer to the pool
                _bufferPoolInternal.ReturnBuffer(_buffer);
            }

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                return _ms.CopyToAsync(destination, bufferSize, cancellationToken);
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                return _ms.EndRead(asyncResult);
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
                _ms.EndWrite(asyncResult);
            }

            public override void Flush()
            {
                _ms.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return _ms.FlushAsync(cancellationToken);
            }

            public override byte[] GetBuffer()
            {
                return _buffer;
            }

            public override long Position
            {
                get { return _ms.Position; }
                set { _ms.Position = value; }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _ms.Read(buffer, offset, count);
            }

            void EnlargeBufferIfRequired(int count)
            {
                // we cannot fit the data into the existing buffer, time for a new buffer
                if (count > (_buffer.Length - _ms.Position))
                {
                    int position = (int)_ms.Position;

                    // double the buffer size
                    int newSize = _buffer.Length * 2;

                    // make sure the new size is big enough
                    int requiredSize = count + _buffer.Length - position;
                    if (requiredSize > newSize)
                    {
                        // compute the power of two larger than requiredSize. so 40000 => 65536
                        newSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(requiredSize) / Math.Log(2)));
                    }

                    byte[] newBuffer = new byte[newSize];
                    Buffer.BlockCopy(_buffer, 0, newBuffer, 0, position);
                    _ms = new MemoryStream(newBuffer, 0, newBuffer.Length, true, true)
                    {
                        Position = position
                    };

                    _buffer = newBuffer;
                }
            }

            public override void WriteByte(byte value)
            {
                EnlargeBufferIfRequired(1);
                _ms.WriteByte(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                EnlargeBufferIfRequired(count);
                _ms.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                EnlargeBufferIfRequired(count);
                return _ms.WriteAsync(buffer, offset, count);
            }

            public override object InitializeLifetimeService()
            {
                return _ms.InitializeLifetimeService();
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _ms.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override int ReadByte()
            {
                return _ms.ReadByte();
            }

            public override int ReadTimeout
            {
                get { return _ms.ReadTimeout; }
                set { _ms.ReadTimeout = value; }
            }

            public override long Seek(long offset, SeekOrigin loc)
            {
                return _ms.Seek(offset, loc);
            }

            /// <summary>
            /// Note: This will not make the MemoryStream any smaller, only larger
            /// </summary>
            public override void SetLength(long value)
            {
                EnlargeBufferIfRequired((int)value);
            }

            public override byte[] ToArray()
            {
                // you should never call this
                return _ms.ToArray();
            }

            public override int WriteTimeout
            {
                get { return _ms.WriteTimeout; }
                set { _ms.WriteTimeout = value; }
            }

#if !NET45
            public override bool TryGetBuffer(out ArraySegment<byte> buffer)
            {
                buffer = new ArraySegment<byte>(_buffer, 0, (int)_ms.Position);
                return true;
            }
#endif

            public override void WriteTo(Stream stream)
            {
                _ms.WriteTo(stream);
            }
        }

        /// <summary>
        /// Gets a MemoryStream built from a buffer plucked from a thread safe pool
        /// The pool grows automatically.
        /// Closing the memory stream clears the buffer and returns it to the pool
        /// </summary>
        public MemoryStream GetBuffer()
        {
            if (!_bufferPoolStack.TryPop(out byte[] buffer))
            {
                buffer = new byte[_bufferSize];
            }

            return new PublicBufferMemoryStream(buffer, this);
        }

        protected void ReturnBuffer(byte[] buffer)
        {
            _bufferPoolStack.Push(buffer);
        }
    }
}
