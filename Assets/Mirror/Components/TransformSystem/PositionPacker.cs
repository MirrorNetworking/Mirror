using System;
using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

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
    }

    public class PositionPacker
    {
        readonly FloatPacker x;
        readonly FloatPacker y;
        readonly FloatPacker z;
        public readonly int bitCount;

        public Vector3Int BitCountAxis => new Vector3Int(x.bitCount, y.bitCount, z.bitCount);

        public PositionPacker(Bounds bounds, float precision)
            : this(bounds.min, bounds.max, Vector3.one * precision) { }
        public PositionPacker(Bounds bounds, Vector3 precision)
            : this(bounds.min, bounds.max, precision) { }
        public PositionPacker(Vector3 min, Vector3 max, float precision)
            : this(min, max, Vector3.one * precision) { }
        public PositionPacker(Vector3 min, Vector3 max, Vector3 precision)
        {
            x = new FloatPacker(min.x, max.x, precision.x);
            y = new FloatPacker(min.y, max.y, precision.y);
            z = new FloatPacker(min.z, max.z, precision.z);

            bitCount = x.bitCount + y.bitCount + z.bitCount;

            const int maxBitCount = sizeof(ulong) * 8;
            if (bitCount > maxBitCount)
            {
                // todo support this. check performance to see if it is event worth compressing over 64 bits
                throw new NotSupportedException($"Compressing to sizes over {maxBitCount}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, Vector3 value)
        {
            x.Pack(writer, value.x);
            y.Pack(writer, value.y);
            z.Pack(writer, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 Unpack(BitReader reader)
        {
            return new Vector3(
                x.Unpack(reader),
                y.Unpack(reader),
                z.Unpack(reader));
        }
    }
}
