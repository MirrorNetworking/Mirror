using System;
using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace JamesFrowen.BitPacking
{
    public class QuaternionPacker
    {
        const float MinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float MaxValue = 1f / 1.414214f;

        readonly int BitLength = 10;
        // same as Mathf.Pow(2, targetBitLength) - 1
        // is also mask
        readonly uint UintMax;

        public readonly int bitCount;

        public QuaternionPacker(int quaternionBitLength)
        {
            BitLength = quaternionBitLength;
            UintMax = (1u << BitLength) - 1u;
            bitCount = 2 + quaternionBitLength * 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, Quaternion _value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            float x = _value.x;
            float y = _value.y;
            float z = _value.z;
            float w = _value.w;

            quickNormalize(ref x, ref y, ref z, ref w);

            FindLargestIndex(x, y, z, w, out int index, out float largest);

            GetSmallerDimensions(index, x, y, z, w, out float a, out float b, out float c);

            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            uint ua = Compression.ScaleToUInt(a, MinValue, MaxValue, 0, UintMax);
            uint ub = Compression.ScaleToUInt(b, MinValue, MaxValue, 0, UintMax);
            uint uc = Compression.ScaleToUInt(c, MinValue, MaxValue, 0, UintMax);

            writer.Write((uint)index, 2);
            writer.Write(ua, BitLength);
            writer.Write(ub, BitLength);
            writer.Write(uc, BitLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void quickNormalize(ref float x, ref float y, ref float z, ref float w)
        {
            float dot =
                x * x +
                y * y +
                z * z +
                w * w;
            const float allowedEpsilon = 1E-5f;
            const float minAllowed = 1 - allowedEpsilon;
            const float maxAllowed = 1 + allowedEpsilon;
            if (minAllowed > dot || maxAllowed < dot)
            {
                float dotSqrt = (float)Math.Sqrt(dot);
                // rotation is 0
                if (dotSqrt < allowedEpsilon)
                {
                    // identity
                    x = 0;
                    y = 0;
                    z = 0;
                    w = 1;
                }
                else
                {
                    x /= dotSqrt;
                    y /= dotSqrt;
                    z /= dotSqrt;
                    w /= dotSqrt;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FindLargestIndex(float x, float y, float z, float w, out int index, out float largest)
        {
            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;
            float w2 = w * w;

            index = 0;
            float current = x2;
            largest = x;
            // check vs sq to avoid doing mathf.abs
            if (y2 > current)
            {
                index = 1;
                largest = y;
                current = y2;
            }
            if (z2 > current)
            {
                index = 2;
                largest = z;
                current = z2;
            }
            if (w2 > current)
            {
                index = 3;
                largest = w;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void GetSmallerDimensions(int largestIndex, float x, float y, float z, float w, out float a, out float b, out float c)
        {
            switch (largestIndex)
            {
                case 0:
                    a = y;
                    b = z;
                    c = w;
                    return;
                case 1:
                    a = x;
                    b = z;
                    c = w;
                    return;
                case 2:
                    a = x;
                    b = y;
                    c = w;
                    return;
                case 3:
                    a = x;
                    b = y;
                    c = z;
                    return;
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion Unpack(BitReader reader)
        {
            Quaternion result;

            uint index = reader.Read(2);
            uint ua = reader.Read(BitLength);
            uint ub = reader.Read(BitLength);
            uint uc = reader.Read(BitLength);

            float a = Compression.ScaleFromUInt(ua, MinValue, MaxValue, 0, UintMax);
            float b = Compression.ScaleFromUInt(ub, MinValue, MaxValue, 0, UintMax);
            float c = Compression.ScaleFromUInt(uc, MinValue, MaxValue, 0, UintMax);

            result = FromSmallerDimensions(index, a, b, c);

            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Quaternion FromSmallerDimensions(uint largestIndex, float a, float b, float c)
        {
            float l2 = 1 - (a * a + b * b + c * c);
            float largest = (float)Math.Sqrt(l2);
            // this Quaternion should already be normallized because of the way that largest is calculated
            // todo create test to validate that result is normalized
            switch (largestIndex)
            {
                case 0:
                    return new Quaternion(largest, a, b, c);
                case 1:
                    return new Quaternion(a, largest, b, c);
                case 2:
                    return new Quaternion(a, b, largest, c);
                case 3:
                    return new Quaternion(a, b, c, largest);
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");

            }
        }
    }
}
