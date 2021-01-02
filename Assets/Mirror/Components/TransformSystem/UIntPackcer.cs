using System;

namespace Mirror.TransformSyncing
{
    public class UIntPackcer
    {
        readonly int smallBitCount;
        readonly int mediumBitCount;
        readonly int largeBitCount;
        // exclusive max
        readonly uint smallMax;
        readonly uint mediumMax;
        readonly uint largeMax;

        public UIntPackcer(int smallBitCount, int mediumBitCount, int largeBitCount)
        {
            smallMax = 1u << smallBitCount;
            mediumMax = 1u << mediumBitCount;
            largeMax = 1u << largeBitCount;

            this.smallBitCount = smallBitCount;
            this.mediumBitCount = mediumBitCount;
            this.largeBitCount = largeBitCount;
        }

        public void Pack(BitWriter writer, uint value)
        {
            if (value < smallMax)
            {
                writer.Write(0, 1);
                writer.Write(value, smallBitCount);
            }
            else if (value < mediumMax)
            {
                writer.Write(0b10, 2);
                writer.Write(value, mediumBitCount);
            }
            else if (value < largeMax)
            {
                writer.Write(0b11, 2);
                writer.Write(value, largeBitCount);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value should be less than large limit");
            }
        }

        public uint UnPack(BitReader reader)
        {
            uint a = reader.Read(1);
            if (a == 0)
            {
                return reader.Read(smallBitCount);
            }
            else
            {
                uint b = reader.Read(1);
                if (b == 0)
                {
                    return reader.Read(mediumBitCount);
                }
                else
                {
                    return reader.Read(largeBitCount);
                }
            }
        }
    }
}
