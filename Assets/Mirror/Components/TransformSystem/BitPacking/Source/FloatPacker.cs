using System.Runtime.CompilerServices;

namespace JamesFrowen.BitPacking
{
    public class FloatPacker
    {
        public readonly float minFloat;
        public readonly float maxFloat;

        public readonly uint minUint;
        public readonly uint maxUint;

        public readonly int bitCount;

        public readonly uint readMask;
        public readonly ulong readMaskLong;

        public FloatPacker(float min, float max, float precision)
        {
            this.minFloat = min;
            this.maxFloat = max;

            var range = max - min;
            var rangeUint = (uint)(range / precision);

            this.bitCount = BitCountHelper.BitCountFromRange(rangeUint);

            this.minUint = 0u;
            this.maxUint = (1u << this.bitCount) - 1u;

            this.readMask = this.maxUint;
            this.readMaskLong = this.maxUint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, float value)
        {
            var v = Compression.ScaleToUInt(value, this.minFloat, this.maxFloat, this.minUint, this.maxUint);
            writer.Write(v, this.bitCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Unpack(BitReader reader)
        {
            var v = reader.Read(this.bitCount);
            return Compression.ScaleFromUInt(v, this.minFloat, this.maxFloat, this.minUint, this.maxUint);
        }

        public override string ToString()
        {
            return $"FloatPacker:[bitCount:{this.bitCount}, float:{this.minFloat}->{this.maxFloat}, uint:{this.minUint}->{this.maxUint}]";
        }
    }
}
