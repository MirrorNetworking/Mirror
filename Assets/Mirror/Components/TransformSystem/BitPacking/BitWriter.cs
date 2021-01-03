using System;
using System.IO;
using System.Runtime.CompilerServices;
using Mirror;

namespace JamesFrowen.BitPacking
{
    public class BitWriter
    {
        private const int WriteSize = 32;
        NetworkWriter writer;

        ulong scratch;
        int bitsInScratch;

        public BitWriter() { }
        public BitWriter(NetworkWriter writer) => Reset(writer);

        public void Reset(NetworkWriter writer)
        {
            scratch = 0;
            bitsInScratch = 0;
            this.writer = writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value, int bits)
        {
            //Console.WriteLine($"{value},{bits}");
            if (bits > WriteSize)
            {
                throw new ArgumentException($"bits must be less than {WriteSize}");
            }

            ulong mask = (1ul << bits) - 1;
            ulong longValue = value & mask;

            scratch |= (longValue << bitsInScratch);

            bitsInScratch += bits;


            if (bitsInScratch >= WriteSize)
            {
                uint toWrite = (uint)scratch;
                writer.WriteBlittable(toWrite);

                scratch >>= WriteSize;
                bitsInScratch -= WriteSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            if (bitsInScratch > 24)
            {
                uint toWrite = (uint)scratch;
                writer.WriteBlittable(toWrite);
            }
            else if (bitsInScratch > 16)
            {
                ushort toWrite1 = (ushort)scratch;
                byte toWrite2 = (byte)(scratch >> 16);
                writer.WriteBlittable(toWrite1);
                writer.WriteBlittable(toWrite2);
            }
            else if (bitsInScratch > 8)
            {
                ushort toWrite = (ushort)scratch;
                writer.WriteBlittable(toWrite);
            }
            else if (bitsInScratch > 0)
            {
                byte toWrite = (byte)scratch;
                writer.WriteBlittable(toWrite);
            }
        }
    }

    public class BitReader
    {
        private const int ReadSize = 32;

        NetworkReader reader;

        ulong scratch;
        int bitsInScratch;
        int numberOfFullScratches;
        int extraBytesInLastScratch;

        public int BitsInScratch => bitsInScratch;

        public BitReader() { }
        public BitReader(NetworkReader reader) => Reset(reader);

        public void Reset(NetworkReader reader)
        {
            this.reader = reader;

            scratch = 0;
            bitsInScratch = 0;

            int total_bytes = reader.Length;
            numberOfFullScratches = total_bytes / sizeof(int);
            extraBytesInLastScratch = total_bytes - (numberOfFullScratches * sizeof(int));
        }

        public uint Read(int bits)
        {
            if (bits > ReadSize)
            {
                throw new ArgumentException($"bits must be less than {ReadSize}");
            }

            if (bits > bitsInScratch)
            {
                ReadScratch(out uint newValue, out int count);
                scratch |= ((ulong)newValue << bitsInScratch);

                bitsInScratch += count;
            }


            // read bits from scatch
            ulong mask = (1ul << bits) - 1;
            ulong value = scratch & mask;


            // remove bits from scatch
            scratch >>= bits;
            bitsInScratch -= bits;

            return (uint)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadScratch(out uint newBits, out int count)
        {
            if (numberOfFullScratches > 0)
            {
                newBits = reader.ReadBlittable<uint>();
                numberOfFullScratches--;
                count = ReadSize;
            }
            else
            {
                if (extraBytesInLastScratch == 0)
                {
                    throw new EndOfStreamException($"No bits left to read");
                }

                if (extraBytesInLastScratch > 3)
                {
                    newBits = reader.Read<uint>();
                    count = 32;
                }
                else if (extraBytesInLastScratch > 2)
                {
                    ushort newBits1 = reader.Read<ushort>();
                    byte newBits2 = reader.Read<byte>();
                    newBits = newBits1 + ((uint)newBits2 << 16);
                    count = 24;
                }
                else if (extraBytesInLastScratch > 1)
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
                extraBytesInLastScratch = 0;
            }
        }
    }
}
