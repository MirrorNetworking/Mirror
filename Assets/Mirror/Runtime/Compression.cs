using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Functions to Compress Quaternions and Floats
    /// </summary>
    /// <remarks>
    /// Uncompressed Quaternion = 32 * 4 = 128 bits => send 16 bytes
    ///
    /// <para>
    ///     Quaternion is always normalized so we drop largest value and re-calculate it.
    ///     We can encode which one is the largest using 2 bits
    ///     <code>
    ///     x^2 + y^2 + z^2 + w^2 = 1
    ///     </code>
    /// </para>
    ///
    /// <para>
    ///     2nd largest value has max size of 1/sqrt(2)
    ///     We can encode the smallest three components in [-1/sqrt(2),+1/sqrt(2)] instead of [-1,+1]
    ///     <code>
    ///     c^2 + c^2 + 0 + 0 = 1
    ///     </code>
    /// </para>
    /// 
    /// <para>
    ///     Sign of largest value doesn't matter
    ///     <code>
    ///     Q * vec3 == (-Q) * vec3
    ///     </code>
    /// </para>
    /// 
    /// <list type="bullet">
    /// <listheader><description>
    ///     RotationPrecision <br/>
    ///     <code>
    ///     2/sqrt(2) / (2^bitCount - 1)
    ///     </code>
    /// </description></listheader>
    /// 
    /// <item><description>
    ///     rotation precision +-0.00138 in range [-1,+1]
    ///     <code>
    ///     10 bits per value
    ///     2 + 10 * 3 = 32 bits => send 4 bytes
    ///     </code>
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// Links for more info:
    /// <br/><see href="https://youtu.be/Z9X4lysFr64">GDC Talk</see>
    /// <br/><see href="https://gafferongames.com/post/snapshot_compression/">Post on Snapshot Compression</see>
    /// </para>
    /// </remarks>
    public static class Compression
    {
        const float QuaternionMinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float QuaternionMaxValue = 1f / 1.414214f;

        const int QuaternionBitLength = 10;
        // same as Mathf.Pow(2, targetBitLength) - 1
        const uint QuaternionUintRange = (1 << QuaternionBitLength) - 1;

        /// <summary>
        /// Used to Compress Quaternion into 4 bytes
        /// </summary>
        public static uint CompressQuaternion(Quaternion value)
        {
            // make sure value is normalized (don't trust user given value, and math here assumes normalized)
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (value[largestIndex] < 0)
            {
                small *= -1;
            }

            uint a = ScaleToUInt(small.x, QuaternionMinValue, QuaternionMaxValue, 0, QuaternionUintRange);
            uint b = ScaleToUInt(small.y, QuaternionMinValue, QuaternionMaxValue, 0, QuaternionUintRange);
            uint c = ScaleToUInt(small.z, QuaternionMinValue, QuaternionMaxValue, 0, QuaternionUintRange);

            // pack each 10 bits and extra 2 bits into uint32
            uint packed = a | b << 10 | c << 20 | (uint)largestIndex << 30;

            return packed;
        }

        internal static int FindLargestIndex(Quaternion q)
        {
            int index = default;
            float current = default;

            // check each value to see which one is largest (ignoring +-)
            for (int i = 0; i < 4; i++)
            {
                float next = Mathf.Abs(q[i]);
                if (next > current)
                {
                    index = i;
                    current = next;
                }
            }

            return index;
        }

        static Vector3 GetSmallerDimensions(int largestIndex, Quaternion value)
        {
            float x = value.x;
            float y = value.y;
            float z = value.z;
            float w = value.w;

            switch (largestIndex)
            {
                case 0:
                    return new Vector3(y, z, w);
                case 1:
                    return new Vector3(x, z, w);
                case 2:
                    return new Vector3(x, y, w);
                case 3:
                    return new Vector3(x, y, z);
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");
            }
        }


        /// <summary>
        /// Used to read a Compressed Quaternion from 4 bytes
        /// <para>Quaternion is normalized</para>
        /// </summary>
        public static Quaternion DecompressQuaternion(uint packed)
        {
            // 10 bits
            const uint mask = 0b11_1111_1111;
            Quaternion result;


            uint a = packed & mask;
            uint b = (packed >> 10) & mask;
            uint c = (packed >> 20) & mask;
            uint largestIndex = (packed >> 30) & mask;

            float x = ScaleFromUInt(a, QuaternionMinValue, QuaternionMaxValue, 0, QuaternionUintRange);
            float y = ScaleFromUInt(b, QuaternionMinValue, QuaternionMaxValue, 0, QuaternionUintRange);
            float z = ScaleFromUInt(c, QuaternionMinValue, QuaternionMaxValue, 0, QuaternionUintRange);

            Vector3 small = new Vector3(x, y, z);
            result = FromSmallerDimensions(largestIndex, small);
            return result;
        }

        static Quaternion FromSmallerDimensions(uint largestIndex, Vector3 smallest)
        {
            float a = smallest.x;
            float b = smallest.y;
            float c = smallest.z;

            float largest = Mathf.Sqrt(1 - a * a - b * b - c * c);
            switch (largestIndex)
            {
                case 0:
                    return new Quaternion(largest, a, b, c).normalized;
                case 1:
                    return new Quaternion(a, largest, b, c).normalized;
                case 2:
                    return new Quaternion(a, b, largest, c).normalized;
                case 3:
                    return new Quaternion(a, b, c, largest).normalized;
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");

            }
        }


        /// <summary>
        /// Scales float from minFloat->maxFloat to minUint->maxUint
        /// <para>values out side of minFloat/maxFloat will return either 0 or maxUint</para>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minFloat"></param>
        /// <param name="maxFloat"></param>
        /// <param name="minUint">should be a power of 2, can be 0</param>
        /// <param name="maxUint">should be a power of 2, for example 1 &lt;&lt; 8 for value to take up 8 bytes</param>
        /// <returns></returns>
        public static uint ScaleToUInt(float value, float minFloat, float maxFloat, uint minUint, uint maxUint)
        {
            // if out of range return min/max
            if (value > maxFloat) { return maxUint; }
            if (value < minFloat) { return minUint; }

            float rangeFloat = maxFloat - minFloat;
            uint rangeUint = maxUint - minUint;

            // scale value to 0->1 (as float)
            float valueRelative = (value - minFloat) / rangeFloat;
            // scale value to uMin->uMax
            float outValue = valueRelative * rangeUint + minUint;

            return (uint)outValue;
        }

        /// <summary>
        /// Scales uint from minUint->maxUint to minFloat->maxFloat 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minFloat"></param>
        /// <param name="maxFloat"></param>
        /// <param name="minUint">should be a power of 2, can be 0</param>
        /// <param name="maxUint">should be a power of 2, for example 1 &lt;&lt; 8 for value to take up 8 bytes</param>
        /// <returns></returns>
        public static float ScaleFromUInt(uint value, float minFloat, float maxFloat, uint minUint, uint maxUint)
        {
            // if out of range return min/max
            if (value > maxUint) { return maxFloat; }
            if (value < minUint) { return minFloat; }

            float rangeFloat = maxFloat - minFloat;
            uint rangeUint = maxUint - minUint;

            // scale value to 0->1 (as float)
            // make sure divide is float
            float valueRelative = (value - minUint) / (float)rangeUint;
            // scale value to fMin->fMax
            float outValue = valueRelative * rangeFloat + minFloat;
            return outValue;
        }
    }
}
