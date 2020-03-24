using UnityEngine;

namespace Mirror
{

    /*
        uncompressed Quaternion = 32 * 4 = 128 bits => send 16 bytes

        Quaternion Always normalized
        x^2 + y^2 + z^2 + w^2 = 1
        can drop largest value and re-calculate it
        encode which is largest using 2 bits

        2nd largest value has max size of 1/sqrt(2)
        c^2 + c^2 + 0 + 0 = 1
        can encode the smallest three components in [-0.707107,+0.707107] instead of [-1,+1]


        Sign of largest value doesnt matter
        Q * vec3 == (-Q) * vec3


        smallest rotation = 2/sqrt(2) / (2^bitCount - 1)

        23 bits per value (32 * 0.707)
        2 + 23 * 3 = 71 bits => send 9 bytes
        smallest rotation +-0.000000169 in range [-1,+1]

        10 bits per value
        2 + 10 * 3 = 32 bits => send 4 bytes
        smallest rotation +-0.00138 in range [-1,+1]

        7 bits per value
        2 + 7 * 3 = 23 bits => send 3 bytes
        smallest rotation +-0.0110 in range [-1,+1]

        Links for more info
        https://youtu.be/Z9X4lysFr64
        https://gafferongames.com/post/snapshot_compression/
         */

    public enum RotationPrecision
    {
        /// <summary>
        /// Send 9 bytes, smallest rotation 0.000000169 in range [-1,+1]
        /// </summary>
        Highest,
        /// <summary>
        /// Send 4 bytes, smallest rotation 0.00138 in range [-1,+1]
        /// </summary>
        Medium,
        /// <summary>
        /// Send 3 bytes, smallest rotation 0.0110 in range [-1,+1]
        /// </summary>
        Low,
        /// <summary>
        /// Don't send rotation
        /// </summary>
        NoRotation,
    };
    public static class QuaternionReadWrite
    {
        public static RotationPrecision DefaultPrecision => RotationPrecision.Medium;
        const float MinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float MaxValue = 1f / 1.414214f;
        const float ValueRange = MaxValue - MinValue;

        /// <summary>
        /// Writes Quaternion using QuaternionReadWrite.DefaultPrecision
        /// <para>Used to Compress Quaternion into [Highest 9 bytes, Medium 4 bytes, Low 3 bytes, NoRotation 0 bytes]</para>
        /// <para>Quaternion is normalized</para>
        /// </summary>
        public static void WriteQuaternion(this NetworkWriter writer, Quaternion value)
        {
            WriteQuaternion(writer, value, DefaultPrecision);
        }

        /// <summary>
        /// Used to Compress Quaternion into [Highest 9 bytes, Medium 4 bytes, Low 3 bytes, NoRotation 0 bytes]
        /// </summary>
        public static void WriteQuaternion(this NetworkWriter writer, Quaternion value, RotationPrecision precision)
        {
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);


            if (precision == RotationPrecision.Highest)
            {
                WriteQuaternionFull(writer, largestIndex, small);
            }
            else if (precision == RotationPrecision.Medium)
            {
                WriteQuaternionHalf(writer, largestIndex, small);
            }
            else if (precision == RotationPrecision.Low)
            {
                WriteQuaternionLow(writer, largestIndex, small);
            }
        }


        /// <summary>
        /// Reads Quaternion using QuaternionReadWrite.DefaultPrecision
        /// <para>Used to read a Compressed Quaternion [Highest 9 bytes, Medium 4 bytes, Low 3 bytes, NoRotation 0 bytes]</para>
        /// <para>Quaternion is normalized</para>
        /// </summary>
        public static Quaternion ReadQuaternion(this NetworkReader reader)
        {
            return ReadQuaternion(reader, DefaultPrecision);
        }

        /// <summary>
        /// Used to read a Compressed Quaternion [Highest 9 bytes, Medium 4 bytes, Low 3 bytes, NoRotation 0 bytes]
        /// <para>Quaternion is normalized</para>
        /// </summary>
        public static Quaternion ReadQuaternion(this NetworkReader reader, RotationPrecision precision)
        {
            if (precision == RotationPrecision.Highest)
            {
                return ReadQuaternionFull(reader);
            }
            else if (precision == RotationPrecision.Medium)
            {
                return ReadQuaternionHalf(reader);
            }
            else if (precision == RotationPrecision.Low)
            {
                return ReadQuaternionLow(reader);
            }
            else
            {
                return Quaternion.identity;
            }
        }


        static void WriteQuaternionFull(NetworkWriter writer, int largestIndex, Vector3 small)
        {
            const int bitLength = 23;
            uint a = ScaleToUInt(small.x, bitLength);
            uint b = ScaleToUInt(small.y, bitLength);
            uint c = ScaleToUInt(small.z, bitLength);

            // pack largestIndex info ab
            a |= (((uint)largestIndex) & 1u) << 23;
            //only move 2 as bit starts in 2nd position
            b |= (((uint)largestIndex) & 2u) << 22;

            writer.WriteByte((byte)a);
            writer.WriteByte((byte)(a >> 8));
            writer.WriteByte((byte)(a >> 16));

            writer.WriteByte((byte)b);
            writer.WriteByte((byte)(b >> 8));
            writer.WriteByte((byte)(b >> 16));

            writer.WriteByte((byte)c);
            writer.WriteByte((byte)(c >> 8));
            writer.WriteByte((byte)(c >> 16));
        }

        static Quaternion ReadQuaternionFull(NetworkReader reader)
        {
            Quaternion result;
            uint a1 = reader.ReadByte();
            uint a2 = reader.ReadByte();
            uint a3 = reader.ReadByte();

            uint b1 = reader.ReadByte();
            uint b2 = reader.ReadByte();
            uint b3 = reader.ReadByte();

            uint c1 = reader.ReadByte();
            uint c2 = reader.ReadByte();
            uint c3 = reader.ReadByte();

            uint a = a1 | (a2 << 8) | (a3 << 16);
            uint b = b1 | (b2 << 8) | (b3 << 16);
            uint c = c1 | (c2 << 8) | (c3 << 16);

            uint largestIndex = a >> 23 | (2 & (b >> 22));

            // removes largestIndex from a 
            a &= ~(1u << 23);
            b &= ~(1u << 23);

            const int bitLength = 23;

            float x = ScaleFromUInt(a, bitLength);
            float y = ScaleFromUInt(b, bitLength);
            float z = ScaleFromUInt(c, bitLength);

            Vector3 small = new Vector3(x, y, z);
            result = FromSmallerDimensions(largestIndex, small);
            return result;
        }


        static void WriteQuaternionHalf(NetworkWriter writer, int largestIndex, Vector3 small)
        {
            const int bitLength = 10;
            uint a = ScaleToUInt(small.x, bitLength);
            uint b = ScaleToUInt(small.y, bitLength);
            uint c = ScaleToUInt(small.z, bitLength);

            // split abc into 8+1 bits, pack extra 1 bit with largestIndex
            uint a1 = byte.MaxValue & a;
            uint a2 = byte.MaxValue & (a >> 8);

            uint b1 = byte.MaxValue & b;
            uint b2 = byte.MaxValue & (b >> 8);

            uint c1 = byte.MaxValue & c;
            uint c2 = byte.MaxValue & (c >> 8);

            uint extra = ((uint)largestIndex) + (a2 << 2) + (b2 << 4) + (c2 << 6);

            writer.WriteByte((byte)a1);
            writer.WriteByte((byte)b1);
            writer.WriteByte((byte)c1);
            writer.WriteByte((byte)extra);
        }

        static Quaternion ReadQuaternionHalf(NetworkReader reader)
        {
            Quaternion result;
            uint a1 = reader.ReadByte();
            uint b1 = reader.ReadByte();
            uint c1 = reader.ReadByte();
            byte extra = reader.ReadByte();

            // first 2 bytes
            uint largestIndex = (uint)(extra & 3);

            // get nth bit, then move to start
            uint a2 = (uint)((extra & (3 << 2)) >> 2);
            uint b2 = (uint)((extra & (3 << 4)) >> 4);
            uint c2 = (uint)((extra & (3 << 6)) >> 6);

            uint a = a1 | (a2 << 8);
            uint b = b1 | (b2 << 8);
            uint c = c1 | (c2 << 8);

            const int bitLength = 10;

            float x = ScaleFromUInt(a, bitLength);
            float y = ScaleFromUInt(b, bitLength);
            float z = ScaleFromUInt(c, bitLength);

            Vector3 small = new Vector3(x, y, z);
            result = FromSmallerDimensions(largestIndex, small);
            return result;
        }


        static void WriteQuaternionLow(NetworkWriter writer, int largestIndex, Vector3 small)
        {
            const int bitLength = 7;
            uint a = ScaleToUInt(small.x, bitLength);
            uint b = ScaleToUInt(small.y, bitLength);
            uint c = ScaleToUInt(small.z, bitLength);

            // first 7 bits of abc,
            uint a1 = 127u & a;
            uint b1 = 127u & b;
            uint c1 = 127u & c;

            // pack largestIndex info ab
            a1 |= (((uint)largestIndex) & 1u) << 7;
            //only move 6 as bit starts in 2nd position
            b1 |= (((uint)largestIndex) & 2u) << 6;

            writer.WriteByte((byte)a1);
            writer.WriteByte((byte)b1);
            writer.WriteByte((byte)c1);
        }

        static Quaternion ReadQuaternionLow(NetworkReader reader)
        {
            Quaternion result;
            uint a1 = reader.ReadByte();
            uint b1 = reader.ReadByte();
            uint c1 = reader.ReadByte();

            // last 2 bits
            uint i1 = 128u & a1;
            uint i2 = 128u & b1;
            uint largestIndex = i1 >> 7 | i2 >> 6;

            // first 7 bits
            uint a = 127u & a1;
            uint b = 127u & b1;
            uint c = 127u & c1;

            const int bitLength = 7;

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
