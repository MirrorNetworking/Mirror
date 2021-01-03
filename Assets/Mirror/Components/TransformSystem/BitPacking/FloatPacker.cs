using System.Runtime.CompilerServices;
using Mirror;

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
            minFloat = min;
            maxFloat = max;

            float range = max - min;
            uint rangeUint = (uint)(range / precision);

            bitCount = BitCountHelper.BitCountFromRange(rangeUint);

            minUint = 0u;
            maxUint = (1u << bitCount) - 1u;

            readMask = maxUint;
            readMaskLong = maxUint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, float value)
        {
            uint v = Compression.ScaleToUInt(value, minFloat, maxFloat, minUint, maxUint);
            writer.Write(v, bitCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Unpack(BitReader reader)
        {
            uint v = reader.Read(bitCount);
            return Compression.ScaleFromUInt(v, minFloat, maxFloat, minUint, maxUint);
        }

        public override string ToString()
        {
            return $"FloatPacker:[bitCount:{bitCount}, float:{minFloat}->{maxFloat}, uint:{minUint}->{maxUint}]";
        }
    }
}
