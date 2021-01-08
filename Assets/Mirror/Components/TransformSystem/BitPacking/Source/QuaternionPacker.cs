using System;
using System.Runtime.CompilerServices;
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
            this.BitLength = quaternionBitLength;
            this.UintMax = (1u << this.BitLength) - 1u;
            this.bitCount = 2 + (quaternionBitLength * 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, Quaternion _value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            var x = _value.x;
            var y = _value.y;
            var z = _value.z;
            var w = _value.w;

            quickNormalize(ref x, ref y, ref z, ref w);

            FindLargestIndex(x, y, z, w, out var index, out var largest);

            GetSmallerDimensions(index, x, y, z, w, out var a, out var b, out var c);

            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            var ua = Compression.ScaleToUInt(a, MinValue, MaxValue, 0, this.UintMax);
            var ub = Compression.ScaleToUInt(b, MinValue, MaxValue, 0, this.UintMax);
            var uc = Compression.ScaleToUInt(c, MinValue, MaxValue, 0, this.UintMax);

            writer.Write((uint)index, 2);
            writer.Write(ua, this.BitLength);
            writer.Write(ub, this.BitLength);
            writer.Write(uc, this.BitLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void quickNormalize(ref float x, ref float y, ref float z, ref float w)
        {
            var dot =
                (x * x) +
                (y * y) +
                (z * z) +
                (w * w);
            const float allowedEpsilon = 1E-5f;
            const float minAllowed = 1 - allowedEpsilon;
            const float maxAllowed = 1 + allowedEpsilon;
            if (minAllowed > dot || maxAllowed < dot)
            {
                var dotSqrt = (float)Math.Sqrt(dot);
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
            var x2 = x * x;
            var y2 = y * y;
            var z2 = z * z;
            var w2 = w * w;

            index = 0;
            var current = x2;
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

            var index = reader.Read(2);
            var ua = reader.Read(this.BitLength);
            var ub = reader.Read(this.BitLength);
            var uc = reader.Read(this.BitLength);

            var a = Compression.ScaleFromUInt(ua, MinValue, MaxValue, 0, this.UintMax);
            var b = Compression.ScaleFromUInt(ub, MinValue, MaxValue, 0, this.UintMax);
            var c = Compression.ScaleFromUInt(uc, MinValue, MaxValue, 0, this.UintMax);

            result = FromSmallerDimensions(index, a, b, c);

            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Quaternion FromSmallerDimensions(uint largestIndex, float a, float b, float c)
        {
            var l2 = 1 - ((a * a) + (b * b) + (c * c));
            var largest = (float)Math.Sqrt(l2);
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
