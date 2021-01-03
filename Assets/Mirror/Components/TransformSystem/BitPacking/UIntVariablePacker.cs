using System;

namespace JamesFrowen.BitPacking
{
    /// <summary>
    /// Packs Uint to different sizes based on value
    /// </summary>
    public class UIntVariablePacker
    {
        readonly int smallBitCount;
        readonly int mediumBitCount;
        readonly int largeBitCount;
        // exclusive max
        readonly uint smallMax;
        readonly uint mediumMax;
        readonly uint largeMax;

        public readonly uint MaxValue;

        public UIntVariablePacker(int smallBitCount, int mediumBitCount, int largeBitCount)
        {
            this.smallBitCount = smallBitCount;
            this.mediumBitCount = mediumBitCount;
            this.largeBitCount = largeBitCount;

            smallMax = 1u << smallBitCount;
            mediumMax = 1u << mediumBitCount;
            largeMax = 1u << largeBitCount;

            MaxValue = largeMax - 1;
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
                writer.Write(1, 1);
                writer.Write(0, 1);
                writer.Write(value, mediumBitCount);
            }
            else if (value < largeMax)
            {
                writer.Write(1, 1);
                writer.Write(1, 1);
                writer.Write(value, largeBitCount);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value should be less than large limit");
            }
        }

        public uint Unpack(BitReader reader)
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
