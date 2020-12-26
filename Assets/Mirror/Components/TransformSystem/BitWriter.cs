using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Mirror.TransformSyncing
{
    public class BitWriter
    {
        private const int ScratchSize = 32;
        NetworkWriter writer;

        ulong scratch;
        int scratch_bits;

        public BitWriter() { }
        public BitWriter(NetworkWriter writer) => Reset(writer);

        public void Reset(NetworkWriter writer)
        {
            scratch = 0;
            scratch_bits = 0;
            this.writer = writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value, int bits)
        {
            if (bits > ScratchSize)
            {
                throw new System.ArgumentException($"bits must be less than {ScratchSize}");
            }

            ulong mask = (1ul << bits) - 1;
            ulong longValue = value & mask;

            scratch |= (longValue << scratch_bits);

            scratch_bits += bits;

            if (scratch_bits >= ScratchSize)
            {
                uint toWrite = (uint)scratch;
                writer.WriteBlittable(toWrite);

                scratch >>= ScratchSize;
                scratch_bits -= ScratchSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            if (scratch_bits > 24)
            {
                uint toWrite = (uint)scratch;
                writer.WriteBlittable(toWrite);
            }
            else if (scratch_bits > 16)
            {
                // todo only write 3 bytes
                uint toWrite = (uint)scratch;
                writer.WriteBlittable(toWrite);
            }
            else if (scratch_bits > 8)
            {
                ushort toWrite = (ushort)scratch;
                writer.WriteBlittable(toWrite);
            }
            else if (scratch_bits > 0)
            {
                byte toWrite = (byte)scratch;
                writer.WriteBlittable(toWrite);
            }
        }
    }
    public class BitReader
    {
        private const int ScratchSize = 32;
        readonly NetworkReader reader;

        ulong scratch;
        int scratch_bits;
        int full_scratches;
        int scratches_extra_bytes;

        public BitReader(NetworkReader reader)
        {
            this.reader = reader;
            int total_bytes = reader.Length;
            full_scratches = total_bytes / sizeof(int);
            scratches_extra_bytes = total_bytes - (full_scratches * sizeof(int));
        }

        public uint Read(int bits)
        {
            if (bits > ScratchSize)
            {
                throw new System.ArgumentException($"bits must be less than {ScratchSize}");
            }

            if (bits > scratch_bits)
            {
                ReadScratch(out uint newValue, out int count);
                scratch |= ((ulong)newValue << scratch_bits);

                scratch_bits += count;
            }


            // read bits from scatch
            ulong mask = (1ul << bits) - 1;
            ulong value = scratch & mask;


            // remove bits from scatch
            scratch >>= bits;
            scratch_bits -= bits;

            return (uint)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadScratch(out uint newBits, out int count)
        {
            if (full_scratches > 0)
            {
                newBits = reader.ReadBlittable<uint>();
                full_scratches--;
                count = ScratchSize;
            }
            else
            {
                if (scratches_extra_bytes == 0)
                {
                    throw new EndOfStreamException($"No bits left to read");
                }

                if (scratches_extra_bytes > 3)
                {
                    newBits = reader.Read<uint>();
                    count = 24 + 8;
                }
                else if (scratches_extra_bytes > 2)
                {
                    // todo only write 3 bytes
                    newBits = reader.Read<uint>();
                    // extra +8 for now, see todo above
                    count = 16 + 8 + 8;
                }
                else if (scratches_extra_bytes > 1)
                {
                    newBits = reader.Read<ushort>();
                    count = 16;
                }
                else
                {
                    newBits = reader.Read<byte>();
                    count = 8;
                }

                // set to 0 after reading
                scratches_extra_bytes = 0;
            }
        }
    }


    public class BitWriterUnsafeBuffer
    {
        private const int ScratchSize = 32;
        byte[] buffer;
        int bufferIndex;

        ulong scratch;
        int scratch_bits;

        public ArraySegment<byte> ToSegment()
        {
            return new ArraySegment<byte>(buffer, 0, bufferIndex);
        }
        public int BufferCount => bufferIndex;

        public BitWriterUnsafeBuffer(int bufferSize)
        {
            buffer = new byte[bufferSize];
        }

        public void Reset()
        {
            scratch = 0;
            scratch_bits = 0;


            Array.Clear(buffer, 0, buffer.Length);
            bufferIndex = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value, int bits)
        {
            if (bits > ScratchSize)
            {
                throw new System.ArgumentException($"bits must be less than {ScratchSize}");
            }

            ulong mask = (1ul << bits) - 1;
            ulong longValue = value & mask;

            scratch |= (longValue << scratch_bits);

            scratch_bits += bits;

            if (scratch_bits >= ScratchSize)
            {
                unsafeWrite();

                bufferIndex += sizeof(uint);

                scratch >>= ScratchSize;
                scratch_bits -= ScratchSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void unsafeWrite()
        {
            uint toWrite = (uint)scratch;
            fixed (byte* ptr = &buffer[bufferIndex])
            {
                // cast buffer to T* pointer, then assign value to the area
                uint* uPtr = (uint*)ptr;
                *uPtr = toWrite;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            const int bitPerByte = 8;

            // nothing left in scratch to write
            if (scratch_bits != 0)
            {
                unsafeWrite();

                // calculate how many extra bits over index*4 are needed
                // +-1 because int is rounded down, this will mean 8 bits is 1 extra bytes
                int extra = ((scratch_bits - 1) / bitPerByte) + 1;

                bufferIndex += extra;
            }
        }
    }
}
