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

            this.smallMax = 1u << smallBitCount;
            this.mediumMax = 1u << mediumBitCount;
            this.largeMax = 1u << largeBitCount;

            this.MaxValue = this.largeMax - 1;
        }

        public void Pack(BitWriter writer, uint value)
        {
            if (value < this.smallMax)
            {
                writer.Write(0, 1);
                writer.Write(value, this.smallBitCount);
            }
            else if (value < this.mediumMax)
            {
                writer.Write(1, 1);
                writer.Write(0, 1);
                writer.Write(value, this.mediumBitCount);
            }
            else if (value < this.largeMax)
            {
                writer.Write(1, 1);
                writer.Write(1, 1);
                writer.Write(value, this.largeBitCount);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value should be less than large limit");
            }
        }

        public uint Unpack(BitReader reader)
        {
            var a = reader.Read(1);
            if (a == 0)
            {
                return reader.Read(this.smallBitCount);
            }
            else
            {
                var b = reader.Read(1);
                if (b == 0)
                {
                    return reader.Read(this.mediumBitCount);
                }
                else
                {
                    return reader.Read(this.largeBitCount);
                }
            }
        }
    }
}
