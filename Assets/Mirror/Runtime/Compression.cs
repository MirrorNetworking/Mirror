using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Functions to Compress Quaternions
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
    ///     Sign of largest value doesnt matter
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
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (value[largestIndex] < 0)
            {
                small *= -1;
            }

            uint a = ScaleToUInt(small.x, QuaternionMinValue, QuaternionMaxValue, QuaternionUintRange);
            uint b = ScaleToUInt(small.y, QuaternionMinValue, QuaternionMaxValue, QuaternionUintRange);
            uint c = ScaleToUInt(small.z, QuaternionMinValue, QuaternionMaxValue, QuaternionUintRange);

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

            float a, b, c;
            switch (largestIndex)
            {
                case 0:
                    a = y;
                    b = z;
                    c = w;
                    break;
                case 1:
                    a = x;
                    b = z;
                    c = w;
                    break;
                case 2:
                    a = x;
                    b = y;
                    c = w;
                    break;
                case 3:
                    a = x;
                    b = y;
                    c = z;
                    break;
                default:
                    return Vector3.zero;
            }

            return new Vector3(a, b, c);
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

            float x = ScaleFromUInt(a, QuaternionMinValue, QuaternionMaxValue, QuaternionUintRange);
            float y = ScaleFromUInt(b, QuaternionMinValue, QuaternionMaxValue, QuaternionUintRange);
            float z = ScaleFromUInt(c, QuaternionMinValue, QuaternionMaxValue, QuaternionUintRange);

            Vector3 small = new Vector3(x, y, z);
            result = FromSmallerDimensions(largestIndex, small);
            return result;
        }
        static Quaternion FromSmallerDimensions(uint largestIndex, Vector3 smallest)
        {
            float a = smallest.x;
            float b = smallest.y;
            float c = smallest.z;

            float x, y, z, w;
            float largest = Mathf.Sqrt(1 - a * a - b * b - c * c);
            switch (largestIndex)
            {
                case 0:
                    x = largest;
                    y = a;
                    z = b;
                    w = c;
                    break;
                case 1:
                    x = a;
                    y = largest;
                    z = b;
                    w = c;
                    break;
                case 2:
                    x = a;
                    y = b;
                    z = largest;
                    w = c;
                    break;
                case 3:
                    x = a;
                    y = b;
                    z = c;
                    w = largest;
                    break;
                default:
                    return Quaternion.identity;
            }
            // 0.999999f is tolerance for Unity's Dot function
            Debug.Assert(x * x + y * y + z * z + w * w < (2 - 0.999999f), "larger than 1");
            return new Quaternion(x, y, z, w).normalized;
        }

        /// <summary>
        /// Scales float from minFloat->maxFloat to 0->maxUint
        /// </summary>
        public static uint ScaleToUInt(float value, float minFloat, float maxFloat, uint maxUint)
        {
            if (value > maxFloat) { return maxUint; }
            if (value < minFloat) { return 0u; }

            // move value to 0->1
            float valueRelative = (value - minFloat) / (maxFloat - minFloat);
            // scale value to 0->uMax
            float outValue = valueRelative * maxUint;

            return (uint)outValue;
        }

        /// <summary>
        /// Scales uint from 0->maxUint to minFloat->maxFloat 
        /// </summary>
        public static float ScaleFromUInt(uint value, float minFloat, float maxFloat, uint maxUint)
        {
            // scale value from 0->uMax to 0->1
            float valueRelative = ((float)value) / maxUint;
            // move value to fMin-> fMax
            float outValue = valueRelative * (maxFloat - minFloat) + minFloat;
            return outValue;
        }
    }
}
