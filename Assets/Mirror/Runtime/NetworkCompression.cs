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
    public static class NetworkCompression
    {
        const float MinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float MaxValue = 1f / 1.414214f;
        const float ValueRange = MaxValue - MinValue;

        /// <summary>
        /// Used to Compress Quaternion into 4 bytes
        /// </summary>
        public static void WriteQuaternion(NetworkWriter writer, Quaternion value)
        {
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);

            const int bitLength = 10;
            uint a = ScaleToUInt(small.x, bitLength);
            uint b = ScaleToUInt(small.y, bitLength);
            uint c = ScaleToUInt(small.z, bitLength);

            // pack each 10 bits and extra 2 bits into uint32
            // it is faster to pack into int32 than byte to save calls on writer

            uint packed = a | b << 10 | c << 20 | (uint)largestIndex << 30;

            writer.WriteUInt32(packed);
        }

        /// <summary>
        /// Used to read a Compressed Quaternion from 4 bytes
        /// <para>Quaternion is normalized</para>
        /// </summary>
        public static Quaternion ReadQuaternion(NetworkReader reader)
        {
            Quaternion result;

            // 10 bits
            const uint mask = 0b11_1111_1111;

            uint packed = reader.ReadUInt32();
            uint a = packed & mask;
            uint b = (packed >> 10) & mask;
            uint c = (packed >> 20) & mask;
            uint largestIndex = (packed >> 30) & mask;

            const int bitLength = 10;

            float x = ScaleFromUInt(a, bitLength);
            float y = ScaleFromUInt(b, bitLength);
            float z = ScaleFromUInt(c, bitLength);

            Vector3 small = new Vector3(x, y, z);
            result = FromSmallerDimensions(largestIndex, small);
            return result;
        }

        internal static int FindLargestIndex(Quaternion q)
        {
            int index = 0;
            float current = Mathf.Abs(q.x);

            for (int i = 1; i < 4; i++)
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
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others
            float largestSign = Mathf.Sign(value[largestIndex]);
            return new Vector3(a, b, c) * largestSign;
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

        static uint ScaleToUInt(float value, int targetBitLength)
        {
            // same as Mathf.Pow(2, targetBitLength) - 1
            int targetRange = (1 << targetBitLength) - 1;

            if (value > MaxValue) { return (uint)targetRange; }
            if (value < MinValue) { return 0; }

            float valueRelative = (value - MinValue) / ValueRange;
            float outValue = valueRelative * targetRange;

            return (uint)outValue;
        }

        static float ScaleFromUInt(uint source, int sourceBitLength)
        {
            // same as Mathf.Pow(2, targetBitLength) - 1
            int sourceRange = (1 << sourceBitLength) - 1;


            float valueRelative = ((float)source) / sourceRange;
            float value = valueRelative * ValueRange + MinValue;
            return value;
        }
    }
}
