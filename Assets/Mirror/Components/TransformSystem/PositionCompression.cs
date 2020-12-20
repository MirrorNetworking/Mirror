using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror.TransformSyncing
{
    public class PositionCompression
    {
        public struct CompressFloat
        {
            public readonly float minFloat;
            public readonly float maxFloat;

            public readonly uint minUint;
            public readonly uint maxUint;

            public readonly int bitCount;

            public readonly uint readMask;
            public readonly ulong readMaskLong;

            public CompressFloat(float min, float max, float precision)
            {
                minFloat = min;
                maxFloat = max;

                float range = max - min;
                float rangeUint = range / precision;

                float logBase10 = Mathf.Log10(rangeUint);
                float logOf2 = Mathf.Log10(2);
                float logBase2 = logBase10 / logOf2;
                bitCount = Mathf.CeilToInt(logBase2);

                minUint = 0u;
                maxUint = (1u << bitCount) - 1u;

                readMask = maxUint;
                readMaskLong = maxUint;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint Compress(float value)
            {
                return Compression.ScaleToUInt(value, minFloat, maxFloat, minUint, maxUint);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Decompress(uint value)
            {
                return Compression.ScaleFromUInt(value, minFloat, maxFloat, minUint, maxUint);
            }
        }

        readonly CompressFloat x;
        readonly CompressFloat y;
        readonly CompressFloat z;
        public readonly int bitCount;

        public Vector3Int BitCountAxis => new Vector3Int(x.bitCount, y.bitCount, z.bitCount);

        public PositionCompression(Bounds bounds, float precision)
            : this(bounds.min, bounds.max, Vector3.one * precision) { }
        public PositionCompression(Bounds bounds, Vector3 precision)
            : this(bounds.min, bounds.max, precision) { }
        public PositionCompression(Vector3 min, Vector3 max, float precision)
            : this(min, max, Vector3.one * precision) { }
        public PositionCompression(Vector3 min, Vector3 max, Vector3 precision)
        {
            x = new CompressFloat(min.x, max.x, precision.x);
            y = new CompressFloat(min.y, max.y, precision.y);
            z = new CompressFloat(min.z, max.z, precision.z);

            bitCount = x.bitCount + y.bitCount + z.bitCount;

            const int maxBitCount = sizeof(ulong) * 8;
            if (bitCount > maxBitCount)
            {
                // todo support this. check performance to see if it is event worth compressing over 64 bits
                throw new NotSupportedException($"Compressing to sizes over {maxBitCount}");
            }
        }

        public void Compress(NetworkWriter writer, Vector3 value)
        {
            uint a = x.Compress(value.x);
            uint b = y.Compress(value.y);
            uint c = z.Compress(value.z);

            if (bitCount <= 32)
            {
                uint s = a | b << x.bitCount | c << (x.bitCount + y.bitCount);
                if (bitCount <= 16)
                {
                    writer.WriteUInt16((ushort)s);
                }
                else if (bitCount <= 24)
                {
                    writer.WriteUInt16((ushort)s);
                    writer.WriteByte((byte)(s >> 16));
                }
                else // 32
                {
                    writer.WriteUInt32(s);
                }
            }
            else
            {
                ulong s = a | ((ulong)b) << x.bitCount | ((ulong)c) << (x.bitCount + y.bitCount);
                if (bitCount <= 40)
                {
                    writer.WriteUInt32((uint)s);
                    writer.WriteByte((byte)(s >> 32));
                }
                else if (bitCount <= 48)
                {
                    ulong s1 = s;
                    ulong s2 = s >> 32;
                    writer.WriteUInt32((uint)s1);
                    writer.WriteUInt16((ushort)s2);
                }
                else if (bitCount <= 56)
                {
                    writer.WriteUInt32((uint)s);
                    writer.WriteUInt16((ushort)(s >> 32));
                    writer.WriteByte((byte)(s >> 48));
                }
                else // 64
                {
                    writer.WriteUInt64(s);
                }
            }
        }
        public Vector3 Decompress(NetworkReader reader)
        {
            uint a;
            uint b;
            uint c;

            if (bitCount <= 32)
            {
                uint s;

                if (bitCount <= 16)
                {
                    s = reader.ReadUInt16();
                }
                else if (bitCount <= 24)
                {
                    uint s1 = reader.ReadUInt16();
                    uint s2 = reader.ReadByte();

                    s = s1 | s2 << 16;
                }
                else // 32
                {
                    s = reader.ReadUInt32();
                }

                a = x.readMask & s;
                b = y.readMask & s >> x.bitCount;
                c = z.readMask & s >> (x.bitCount + y.bitCount);
            }
            else
            {
                ulong s;

                if (bitCount <= 40)
                {
                    ulong s1 = reader.ReadUInt32();
                    ulong s2 = reader.ReadByte();
                    s = s1 | s2 << 32;
                }
                else if (bitCount <= 48)
                {
                    ulong s1 = reader.ReadUInt32();
                    ulong s2 = reader.ReadUInt16();
                    s = s1 | s2 << 32;
                }
                else if (bitCount <= 56)
                {
                    ulong s1 = reader.ReadUInt32();
                    ulong s2 = reader.ReadUInt16();
                    ulong s3 = reader.ReadByte();
                    s = s1 | s2 << 32 | s3 << 48;
                }
                else // 64
                {
                    s = reader.ReadUInt64();
                }

                a = (uint)(x.readMaskLong & s);
                b = (uint)(y.readMaskLong & (s >> x.bitCount));
                c = (uint)(z.readMaskLong & (s >> (x.bitCount + y.bitCount)));
            }

            return new Vector3(
                 x.Decompress(a),
                 y.Decompress(b),
                 z.Decompress(c));
        }
    }
}
