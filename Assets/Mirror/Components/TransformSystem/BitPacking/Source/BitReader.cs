using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace JamesFrowen.BitPacking
{
    public class BitReader
    {
        // todo allow this to work with pooling

        private const int ReadSize = 32;

        private byte[] buffer;
        private int startOffset;
        private int readOffset;

        ulong scratch;
        int bitsInScratch;
        int numberOfFullScratches;
        int extraBytesInLastScratch;

        public int BitsInScratch => this.bitsInScratch;

        public int Position => this.readOffset;

        public BitReader(byte[] buffer, int offset, int byteLength)
        {
            this.buffer = buffer;
            this.startOffset = offset;
            this.numberOfFullScratches = byteLength / sizeof(int);
            this.extraBytesInLastScratch = byteLength - (this.numberOfFullScratches * sizeof(int));
        }

        public BitReader(ArraySegment<byte> arraySegment) : this(arraySegment.Array, arraySegment.Offset, arraySegment.Count) { }

        // todo do we need this method if array has to be given each time?
        //public void Reset()
        //{
        //    this.scratch = 0;
        //    this.bitsInScratch = 0;

        //    int total_bytes = reader.Length;
        //    this.numberOfFullScratches = total_bytes / sizeof(int);
        //    this.extraBytesInLastScratch = total_bytes - (this.numberOfFullScratches * sizeof(int));
        //}

        public uint Read(int bits)
        {
            if (bits > ReadSize)
            {
                throw new ArgumentException($"bits must be less than {ReadSize}");
            }

            if (bits > this.bitsInScratch)
            {
                this.ReadScratch(out var newValue, out var count);
                this.scratch |= ((ulong)newValue << this.bitsInScratch);

                this.bitsInScratch += count;
            }


            // read bits from scatch
            var mask = (1ul << bits) - 1;
            var value = this.scratch & mask;


            // remove bits from scatch
            this.scratch >>= bits;
            this.bitsInScratch -= bits;

            return (uint)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadScratch(out uint newBits, out int count)
        {
            if (this.numberOfFullScratches > 0)
            {
                newBits = this.read32bitToBuffer();
                this.numberOfFullScratches--;
                count = ReadSize;
            }
            else
            {
                if (this.extraBytesInLastScratch == 0)
                {
                    throw new EndOfStreamException($"No bits left to read");
                }

                if (this.extraBytesInLastScratch > 3)
                {
                    newBits = this.read32bitToBuffer();
                    count = 32;
                }
                else if (this.extraBytesInLastScratch > 2)
                {
                    newBits = this.read24bitToBuffer();
                    count = 24;
                }
                else if (this.extraBytesInLastScratch > 1)
                {
                    newBits = this.read16bitToBuffer();
                    count = 16;
                }
                else
                {
                    newBits = this.read8bitToBuffer();
                    count = 8;
                }

                // set to 0 after reading
                this.extraBytesInLastScratch = 0;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint read32bitToBuffer()
        {
            var offset = this.startOffset + this.readOffset;
            this.readOffset += 4;
            return this.buffer[offset]
                | ((uint)this.buffer[offset + 1] << 8)
                | ((uint)this.buffer[offset + 2] << 16)
                | ((uint)this.buffer[offset + 3] << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint read24bitToBuffer()
        {
            var offset = this.startOffset + this.readOffset;
            this.readOffset += 3;
            return this.buffer[offset]
                | ((uint)this.buffer[offset + 1] << 8)
                | ((uint)this.buffer[offset + 2] << 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint read16bitToBuffer()
        {
            var offset = this.startOffset + this.readOffset;
            this.readOffset += 2;
            return this.buffer[offset]
                | ((uint)this.buffer[offset + 1] << 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint read8bitToBuffer()
        {
            var offset = this.startOffset + this.readOffset;
            this.readOffset += 1;
            return this.buffer[offset];
        }
    }
}
