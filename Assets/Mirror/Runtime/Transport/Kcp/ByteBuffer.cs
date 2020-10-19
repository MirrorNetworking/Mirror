// byte[] buffer with Position, resizes automatically.
// There is no size limit because we will only use it with ~MTU sized arrays.
using System;
using System.Runtime.CompilerServices;

namespace Mirror.KCP
{
    public class ByteBuffer
    {
        public int Position;
        internal const int InitialCapacity = 1200;
        public byte[] RawBuffer = new byte[InitialCapacity];

        // resize to 'value' capacity if needed
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureCapacity(int value)
        {
            if (RawBuffer.Length < value)
            {
                int capacity = Math.Max(value, RawBuffer.Length * 2);
                Array.Resize(ref RawBuffer, capacity);
            }
        }

        // write bytes at offset
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(byte[] bytes, int offset, int count)
        {
            if (offset >= 0 && count > 0)
            {
                EnsureCapacity(Position + count);
                Buffer.BlockCopy(bytes, offset, RawBuffer, Position, count);
                Position += count;
            }
        }
    }
}
