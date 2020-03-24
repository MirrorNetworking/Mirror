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


        With no precision loss 

        22 bits per value (32 * 0.707)
        2 + 22 * 3 = 70 bits => send 9 bytes

        9 bits per value
        2 + 9 * 3 = 29 bits => send 4 bytes

        Links for more info
        https://youtu.be/Z9X4lysFr64
        https://gafferongames.com/post/snapshot_compression/
         */

    public enum RotationPrecision
    {
        /// <summary>
        /// Send 9 bytes, smallest rotation 0.000000334
        /// </summary>
        Full,
        /// <summary>
        /// Send 4 bytes, smallest rotation 0.00276
        /// </summary>
        Half,
        /// <summary>
        /// Dont send rotation
        /// </summary>
        NoRotation,
    };
    public static class QuaternionReadWrite
    {
        public static RotationPrecision DefaultPrecision => RotationPrecision.Half;
        const float MinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float MaxValue = 1f / 1.414214f;
        const float ValueRange = MaxValue - MinValue;

        /// <summary>
        /// Writes Quaternion using QuaternionReadWrite.DefaultPrecision
        /// </summary>
        public static void WriteQuaternion(this NetworkWriter writer, Quaternion value)
        {
            WriteQuaternion(writer, value, DefaultPrecision);
        }
        public static void WriteQuaternion(this NetworkWriter writer, Quaternion value, RotationPrecision precision)
        {
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);


            if (precision == RotationPrecision.Full)
            {
                WriteQuaternionFull(writer, largestIndex, small);
            }
            else if (precision == RotationPrecision.Half)
            {
                WriteQuaternionHalf(writer, largestIndex, small);
            }
        }

        /// <summary>
        /// Writes Quaternion using QuaternionReadWrite.DefaultPrecision
        /// </summary>
        public static Quaternion ReadQuaternion(this NetworkReader reader)
        {
            return ReadQuaternion(reader, DefaultPrecision);
        }
        public static Quaternion ReadQuaternion(this NetworkReader reader, RotationPrecision precision)
        {
            if (precision == RotationPrecision.Full)
            {
                return ReadQuaternionFull(reader);
            }
            else if (precision == RotationPrecision.Half)
            {
                return ReadQuaternionHalf(reader);
            }
            else
            {
                return Quaternion.identity;
            }
        }

        static void WriteQuaternionFull(NetworkWriter writer, int largestIndex, Vector3 small)
        {
            // 22 bits
            int bitLength = 22;
            uint a = ScaleToUInt(small.x, bitLength);
            uint b = ScaleToUInt(small.y, bitLength);
            uint c = ScaleToUInt(small.z, bitLength);

            // pack largestIndex info a
            a |= ((uint)largestIndex) << 22;

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

            uint largestIndex = a >> 22;
            // removes largestIndex from a 
            a &= ~(3u << 22);

            const int bitLength = 22;

            float x = ScaleFromUInt(a, bitLength);
            float y = ScaleFromUInt(b, bitLength);
            float z = ScaleFromUInt(c, bitLength);

            Vector3 small = new Vector3(x, y, z);
            result = FromSmallerDimensions(largestIndex, small);
            return result;
        }

        static void WriteQuaternionHalf(NetworkWriter writer, int largestIndex, Vector3 small)
        {
            // 22 bits
            int bitLength = 9;
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

            uint extra = ((uint)largestIndex) + (a2 << 2) + (b2 << 3) + (c2 << 4);

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
            uint a2 = (uint)((extra & (1 << 2)) >> 2);
            uint b2 = (uint)((extra & (1 << 3)) >> 3);
            uint c2 = (uint)((extra & (1 << 4)) >> 4);

            uint a = a1 | (a2 << 8);
            uint b = b1 | (b2 << 8);
            uint c = c1 | (c2 << 8);

            const int bitLength = 9;

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
                    throw new System.ArgumentException("LargestIndex did not have value between 0 and 3");
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
                    throw new System.ArgumentException("LargestIndex did not have value between 0 and 3");
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

            float valueRetive = (value - MinValue) / ValueRange;
            float outValue = valueRetive * targetRange;

            return (uint)outValue;
        }
        static float ScaleFromUInt(uint source, int sourceBitLength)
        {
            // same as Mathf.Pow(2, targetBitLength) - 1
            int sourceRange = (1 << sourceBitLength) - 1;


            float valueRetive = ((float)source) / sourceRange;
            float value = valueRetive * ValueRange + MinValue;
            return value;
        }
    }
}
