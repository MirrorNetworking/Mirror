using System;
using UnityEngine;

namespace Mirror
{
    enum ComponentType : uint
    {
        X = 0,
        Y = 1,
        Z = 2,
        W = 3
    }

    /// <summary>Functions to Compress Quaternions and Floats</summary>
    /// <remarks>
    ///     Credit to this man for converting gaffer games c code to c#
    ///     https://gist.github.com/fversnel/0497ad7ab3b81e0dc1dd
    /// </remarks>
    public static class Compression
    {
        // note: 1.0f / sqrt(2)
        private const float Maximum = +1.0f / 1.414214f;

        private const int BitsPerAxis = 10;
        private const int LargestComponentShift = BitsPerAxis * 3;
        private const int AShift = BitsPerAxis * 2;
        private const int BShift = BitsPerAxis * 1;
        private const int IntScale = (1 << (BitsPerAxis - 1)) - 1;
        private const int IntMask = (1 << BitsPerAxis) - 1;

        public static uint CompressQuaternion(Quaternion quaternion)
        {
            float absX = Mathf.Abs(quaternion.x);
            float absY = Mathf.Abs(quaternion.y);
            float absZ = Mathf.Abs(quaternion.z);
            float absW = Mathf.Abs(quaternion.w);

            ComponentType largestComponent = ComponentType.X;
            float largestAbs = absX;
            float largest = quaternion.x;

            if (absY > largestAbs)
            {
                largestAbs = absY;
                largestComponent = ComponentType.Y;
                largest = quaternion.y;
            }
            if (absZ > largestAbs)
            {
                largestAbs = absZ;
                largestComponent = ComponentType.Z;
                largest = quaternion.z;
            }
            if (absW > largestAbs)
            {
                largestComponent = ComponentType.W;
                largest = quaternion.w;
            }

            float a = 0;
            float b = 0;
            float c = 0;
            switch (largestComponent)
            {
                case ComponentType.X:
                    a = quaternion.y;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Y:
                    a = quaternion.x;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Z:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.w;
                    break;
                case ComponentType.W:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.z;
                    break;
            }

            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            uint integerA = ScaleToUint(a);
            uint integerB = ScaleToUint(b);
            uint integerC = ScaleToUint(c);

            return (((uint)largestComponent) << LargestComponentShift) | (integerA << AShift) | (integerB << BShift) | integerC;
        }

        internal static uint ScaleToUint(float v)
        {
            float normalized = v / Maximum;
            return (uint)Mathf.RoundToInt(normalized * IntScale) & IntMask;
        }

        internal static float ScaleToFloat(uint v)
        {
            float unscaled = v * Maximum / IntScale;

            if (unscaled > Maximum)
                unscaled -= Maximum * 2;
            return unscaled;
        }

        public static Quaternion DecompressQuaternion(uint compressed)
        {
            var largestComponentType = (ComponentType)(compressed >> LargestComponentShift);
            uint integerA = (compressed >> AShift) & IntMask;
            uint integerB = (compressed >> BShift) & IntMask;
            uint integerC = compressed & IntMask;

            float a = ScaleToFloat(integerA);
            float b = ScaleToFloat(integerB);
            float c = ScaleToFloat(integerC);

            Quaternion rotation;
            switch (largestComponentType)
            {
                case ComponentType.X:
                    // (?) y z w
                    rotation.y = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.x = Mathf.Sqrt(1 - rotation.y * rotation.y
                                              - rotation.z * rotation.z
                                              - rotation.w * rotation.w);
                    break;
                case ComponentType.Y:
                    // x (?) z w
                    rotation.x = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.y = Mathf.Sqrt(1 - rotation.x * rotation.x
                                              - rotation.z * rotation.z
                                              - rotation.w * rotation.w);
                    break;
                case ComponentType.Z:
                    // x y (?) w
                    rotation.x = a;
                    rotation.y = b;
                    rotation.w = c;
                    rotation.z = Mathf.Sqrt(1 - rotation.x * rotation.x
                                              - rotation.y * rotation.y
                                              - rotation.w * rotation.w);
                    break;
                case ComponentType.W:
                    // x y z (?)
                    rotation.x = a;
                    rotation.y = b;
                    rotation.z = c;
                    rotation.w = Mathf.Sqrt(1 - rotation.x * rotation.x
                                              - rotation.y * rotation.y
                                              - rotation.z * rotation.z);
                    break;
                default:
                    // Should never happen!
                    throw new ArgumentOutOfRangeException("Unknown rotation component type: " + largestComponentType);
            }

            return rotation;
        }

        // varint compression //////////////////////////////////////////////////
        // compress ulong varint.
        // same result for int, short and byte. only need one function.
        // NOT an extension. otherwise weaver might accidentally use it.
        public static void CompressVarUInt(NetworkWriter writer, ulong value)
        {
            if (value <= 240)
            {
                writer.Write((byte)value);
                return;
            }
            if (value <= 2287)
            {
                writer.Write((byte)(((value - 240) >> 8) + 241));
                writer.Write((byte)((value - 240) & 0xFF));
                return;
            }
            if (value <= 67823)
            {
                writer.Write((byte)249);
                writer.Write((byte)((value - 2288) >> 8));
                writer.Write((byte)((value - 2288) & 0xFF));
                return;
            }
            if (value <= 16777215)
            {
                writer.Write((byte)250);
                writer.Write((byte)(value & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                writer.Write((byte)251);
                writer.Write((byte)(value & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                writer.Write((byte)252);
                writer.Write((byte)(value & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 24) & 0xFF));
                writer.Write((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                writer.Write((byte)253);
                writer.Write((byte)(value & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 24) & 0xFF));
                writer.Write((byte)((value >> 32) & 0xFF));
                writer.Write((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                writer.Write((byte)254);
                writer.Write((byte)(value & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 24) & 0xFF));
                writer.Write((byte)((value >> 32) & 0xFF));
                writer.Write((byte)((value >> 40) & 0xFF));
                writer.Write((byte)((value >> 48) & 0xFF));
                return;
            }

            // all others
            {
                writer.Write((byte)255);
                writer.Write((byte)(value & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 24) & 0xFF));
                writer.Write((byte)((value >> 32) & 0xFF));
                writer.Write((byte)((value >> 40) & 0xFF));
                writer.Write((byte)((value >> 48) & 0xFF));
                writer.Write((byte)((value >> 56) & 0xFF));
            }
        }


        // zigzag encoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public static void CompressVarInt(NetworkWriter writer, long i)
        {
            ulong zigzagged = (ulong)((i >> 63) ^ (i << 1));
            CompressVarUInt(writer, zigzagged);
        }

        // NOT an extension. otherwise weaver might accidentally use it.
        public static ulong DecompressVarUInt(NetworkReader reader)
        {
            byte a0 = reader.ReadByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = reader.ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + ((a0 - (ulong)241) << 8) + a1;
            }

            byte a2 = reader.ReadByte();
            if (a0 == 249)
            {
                return 2288 + ((ulong)a1 << 8) + a2;
            }

            byte a3 = reader.ReadByte();
            if (a0 == 250)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16);
            }

            byte a4 = reader.ReadByte();
            if (a0 == 251)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24);
            }

            byte a5 = reader.ReadByte();
            if (a0 == 252)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32);
            }

            byte a6 = reader.ReadByte();
            if (a0 == 253)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40);
            }

            byte a7 = reader.ReadByte();
            if (a0 == 254)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48);
            }

            byte a8 = reader.ReadByte();
            if (a0 == 255)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48)  + (((ulong)a8) << 56);
            }

            throw new IndexOutOfRangeException("DecompressVarInt failure: " + a0);
        }

        // zigzag decoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public static long DecompressVarInt(NetworkReader reader)
        {
            ulong data = DecompressVarUInt(reader);
            return ((long)(data >> 1)) ^ -((long)data & 1);
        }
    }
}
