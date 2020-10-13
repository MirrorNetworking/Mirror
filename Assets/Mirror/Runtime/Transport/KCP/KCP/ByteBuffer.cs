using System;
using System.Collections.Generic;

namespace Mirror.KCP
{
    class ByteBuffer : IDisposable
    {
        int writeIndex;
        static readonly List<ByteBuffer> pool = new List<ByteBuffer>();
        const int PoolMaxCount = 200;

        ByteBuffer(int capacity)
        {
            RawBuffer = new byte[capacity];
            Capacity = capacity;
            ReaderIndex = 0;
            writeIndex = 0;
        }

        /// <summary>
        /// Construct a capacity length byte buffer ByteBuffer object
        /// </summary>
        /// <param name="capacity">Initial capacity</param>
        /// <param name="fromPool">
        /// true means to obtain a pooled ByteBuffer object, the pooled object must be pushed into the pool after calling Dispose, this method is thread-safe.
        /// When true, the actual capacity value of the object obtained from the pool
        /// </param>
        /// <returns>ByteBuffer object</returns>
        public static ByteBuffer Allocate(int capacity)
        {
            ByteBuffer bbuf;
            if (pool.Count == 0)
            {
                bbuf = new ByteBuffer(capacity);
                return bbuf;
            }
            int lastIndex = pool.Count - 1;
            bbuf = pool[lastIndex];
            pool.RemoveAt(lastIndex);
            return bbuf;
            
        }

        /// <summary>
        /// According to the value, determine the nearest 2nd power greater than this length, such as length=7, the return value is 8; length=12, then 16
        /// </summary>
        /// <param name="value">Reference capacity</param>
        /// <returns>The nearest second power greater than the reference capacity</returns>
        int FixLength(int value)
        {
            if (value == 0)
            {
                return 1;
            }
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        /// <summary>
        /// Determine the size of the internal byte buffer array
        /// </summary>
        /// <param name="currLen">Current capacity</param>
        /// <param name="futureLen">Future capacity</param>
        /// <returns>The maximum capacity of the current buffer</returns>
        void FixSizeAndReset(int currLen, int futureLen)
        {
            if (futureLen > currLen)
            {
                //Determine the size of the internal byte buffer with twice the original size to the power of 2
                int size = FixLength(currLen) * 2;
                if (futureLen > size)
                {
                    //Determine the size of the internal byte buffer by twice the power of the future size
                    size = FixLength(futureLen) * 2;
                }
                byte[] newbuf = new byte[size];
                Array.Copy(RawBuffer, 0, newbuf, 0, currLen);
                RawBuffer = newbuf;
                Capacity = size;
            }
        }

        /// <summary>
        /// Write length bytes starting from startIndex of bytes byte array to this buffer
        /// </summary>
        /// <param name="bytes">Byte data to be written</param>
        /// <param name="startIndex">Start position of writing</param>
        /// <param name="length">Length written</param>
        public void WriteBytes(byte[] bytes, int startIndex, int length)
        {
            if (length <= 0 || startIndex < 0) return;

            int total = length + writeIndex;
            int len = RawBuffer.Length;
            FixSizeAndReset(len, total);
            Array.Copy(bytes, startIndex, RawBuffer, writeIndex, length);
            writeIndex = total;
        }

        public int ReaderIndex { get; private set; }

        public int ReadableBytes => writeIndex - ReaderIndex;

        public int Capacity { get; private set; }

        public byte[] RawBuffer { get; private set; }

        public void Clear()
        {
            ReaderIndex = 0;
            writeIndex = 0;
            Capacity = RawBuffer.Length;
        }

        public void Dispose()
        {
            if (pool.Count < PoolMaxCount)
            {
                Clear();
                pool.Add(this);
                return;
            }
            ReaderIndex = 0;
            writeIndex = 0;
            Capacity = 0;
            RawBuffer = null;
        }
    }
}
