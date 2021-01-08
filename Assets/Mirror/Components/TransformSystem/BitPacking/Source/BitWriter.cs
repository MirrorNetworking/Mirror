using System;
using System.Runtime.CompilerServices;

namespace JamesFrowen.BitPacking
{
    public class BitWriter
    {
        // todo allow this to work with pooling
        // todo try writing to buffer directly instead of using scratch

        private const int WriteSize = 32;

        byte[] buffer;
        int writeCount;

        ulong scratch;
        int bitsInScratch;

        public int Length => this.writeCount;

        public BitWriter(int bufferSize) : this(new byte[bufferSize]) { }
        public BitWriter(byte[] buffer)
        {
            this.buffer = buffer;
        }

        public void Reset()
        {
            this.scratch = 0;
            this.bitsInScratch = 0;
            // +1 because last might not be full word
            Array.Clear(this.buffer, 0, this.writeCount);
            this.writeCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value, int bits)
        {
            //Console.WriteLine($"{value},{bits}");
            if (bits > WriteSize)
            {
                throw new ArgumentException($"bits must be less than {WriteSize}");
            }

            var mask = (1ul << bits) - 1;
            var longValue = value & mask;

            this.scratch |= (longValue << this.bitsInScratch);

            this.bitsInScratch += bits;


            if (this.bitsInScratch >= WriteSize)
            {
                var toWrite = (uint)this.scratch;
                this.write32bitToBuffer(toWrite);

                this.scratch >>= WriteSize;
                this.bitsInScratch -= WriteSize;
            }
        }

        public void Flush()
        {
            var toWrite = (uint)this.scratch;
            if (this.bitsInScratch > 24)
            {
                this.write32bitToBuffer(toWrite);
            }
            else if (this.bitsInScratch > 16)
            {
                this.write24bitToBuffer(toWrite);
            }
            else if (this.bitsInScratch > 8)
            {
                this.write16bitToBuffer(toWrite);
            }
            else if (this.bitsInScratch > 0)
            {
                this.write8bitToBuffer(toWrite);
            }

            // set to 0 incase flush is called twice
            this.bitsInScratch = 0;
        }
        public ArraySegment<byte> ToArraySegment()
        {
            this.Flush();
            return new ArraySegment<byte>(this.buffer, 0, this.writeCount);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void write32bitToBuffer(uint toWrite)
        {
            this.buffer[this.writeCount] = (byte)(toWrite);
            this.buffer[this.writeCount + 1] = (byte)(toWrite >> 8);
            this.buffer[this.writeCount + 2] = (byte)(toWrite >> 16);
            this.buffer[this.writeCount + 3] = (byte)(toWrite >> 24);
            this.writeCount += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void write24bitToBuffer(uint toWrite)
        {
            this.buffer[this.writeCount] = (byte)(toWrite);
            this.buffer[this.writeCount + 1] = (byte)(toWrite >> 8);
            this.buffer[this.writeCount + 2] = (byte)(toWrite >> 16);
            this.writeCount += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void write16bitToBuffer(uint toWrite)
        {
            this.buffer[this.writeCount] = (byte)(toWrite);
            this.buffer[this.writeCount + 1] = (byte)(toWrite >> 8);
            this.writeCount += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void write8bitToBuffer(uint toWrite)
        {
            this.buffer[this.writeCount] = (byte)(toWrite);
            this.writeCount += 1;
        }
    }
}
