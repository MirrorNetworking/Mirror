using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// a class that holds writers for the different types
    /// Note that c# creates a different static variable for each
    /// type
    /// This will be populated by the weaver
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class Writer<T>
    {
        public static Action<NetworkWriter, T> write;
    }

    /// <summary>
    /// Binary stream Writer. Supports simple types, buffers, arrays, structs, and nested types
    /// <para>Use <see cref="NetworkWriterPool.GetWriter">NetworkWriter.GetWriter</see> to reduce memory allocation</para>
    /// </summary>
    public class NetworkWriter
    {
        public const int MaxStringLength = 1024 * 32;

        // create writer immediately with it's own buffer so no one can mess with it and so that we can resize it.
        // note: BinaryWriter allocates too much, so we only use a MemoryStream
        // => 1500 bytes by default because on average, most packets will be <= MTU
        byte[] buffer = new byte[1500];

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        int position;
        int length;

        public int Length => length;

        public int Position
        {
            get => position;
            set
            {
                position = value;
                EnsureLength(value);
            }
        }

        /// <summary>
        /// Reset both the position and length of the stream
        /// </summary>
        /// <remarks>
        /// Leaves the capacity the same so that we can reuse this writer without extra allocations
        /// </remarks>
        public void Reset()
        {
            position = 0;
            length = 0;
        }

        /// <summary>
        /// Sets length, moves position if it is greater than new length
        /// </summary>
        /// <param name="newLength"></param>
        /// <remarks>
        /// Zeros out any extra length created by setlength
        /// </remarks>
        public void SetLength(int newLength)
        {
            int oldLength = length;

            // ensure length & capacity
            EnsureLength(newLength);

            // zero out new length
            if (oldLength < newLength)
            {
                Array.Clear(buffer, oldLength, newLength - oldLength);
            }

            length = newLength;
            position = Mathf.Min(position, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureLength(int value)
        {
            if (length < value)
            {
                length = value;
                EnsureCapacity(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureCapacity(int value)
        {
            if (buffer.Length < value)
            {
                int capacity = Math.Max(value, buffer.Length * 2);
                Array.Resize(ref buffer, capacity);
            }
        }

        // MemoryStream has 3 values: Position, Length and Capacity.
        // Position is used to indicate where we are writing
        // Length is how much data we have written
        // capacity is how much memory we have allocated
        // ToArray returns all the data we have written,  regardless of the current position
        public byte[] ToArray()
        {
            byte[] data = new byte[length];
            Array.ConstrainedCopy(buffer, 0, data, 0, length);
            return data;
        }

        // Gets the serialized data in an ArraySegment<byte>
        // this is similar to ToArray(),  but it gets the data in O(1)
        // and without allocations.
        // Do not write anything else or modify the NetworkWriter
        // while you are using the ArraySegment
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, length);
        }

        public void WriteByte(byte value)
        {
            EnsureLength(position + 1);
            buffer[position++] = value;
        }


        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            EnsureLength(position + count);
            Array.ConstrainedCopy(buffer, offset, this.buffer, position, count);
            position += count;
        }

        public void WriteUInt32(uint value)
        {
            EnsureLength(position + 4);
            buffer[position++] = (byte)value;
            buffer[position++] = (byte)(value >> 8);
            buffer[position++] = (byte)(value >> 16);
            buffer[position++] = (byte)(value >> 24);
        }

        public void WriteInt32(int value) => WriteUInt32((uint)value);

        public void WriteUInt64(ulong value)
        {
            EnsureLength(position + 8);
            buffer[position++] = (byte)value;
            buffer[position++] = (byte)(value >> 8);
            buffer[position++] = (byte)(value >> 16);
            buffer[position++] = (byte)(value >> 24);
            buffer[position++] = (byte)(value >> 32);
            buffer[position++] = (byte)(value >> 40);
            buffer[position++] = (byte)(value >> 48);
            buffer[position++] = (byte)(value >> 56);
        }

        public void WriteInt64(long value) => WriteUInt64((ulong)value);

        /// <summary>
        /// Writes any type that mirror supports
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        public void Write<T>(T value)
        {
            Writer<T>.write(this, value);
        }
    }


    // Mirror's Weaver automatically detects all NetworkWriter function types,
    // but they do all need to be extensions.
    public static class NetworkWriterExtensions
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkWriterExtensions));

        // cache encoding instead of creating it with BinaryWriter each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        static readonly byte[] stringBuffer = new byte[NetworkWriter.MaxStringLength];

        public static void WriteByte(this NetworkWriter writer, byte value) => writer.WriteByte(value);

        public static void WriteSByte(this NetworkWriter writer, sbyte value) => writer.WriteByte((byte)value);

        public static void WriteChar(this NetworkWriter writer, char value) => writer.WriteUInt16(value);

        public static void WriteBoolean(this NetworkWriter writer, bool value) => writer.WriteByte((byte)(value ? 1 : 0));

        public static void WriteUInt16(this NetworkWriter writer, ushort value)
        {
            writer.WriteByte((byte)value);
            writer.WriteByte((byte)(value >> 8));
        }

        public static void WriteInt16(this NetworkWriter writer, short value) => writer.WriteUInt16((ushort)value);

        public static void WriteSingle(this NetworkWriter writer, float value)
        {
            UIntFloat converter = new UIntFloat
            {
                floatValue = value
            };
            writer.WriteUInt32(converter.intValue);
        }

        public static void WriteDouble(this NetworkWriter writer, double value)
        {
            UIntDouble converter = new UIntDouble
            {
                doubleValue = value
            };
            writer.WriteUInt64(converter.longValue);
        }

        public static void WriteDecimal(this NetworkWriter writer, decimal value)
        {
            // the only way to read it without allocations is to both read and
            // write it with the FloatConverter (which is not binary compatible
            // to writer.Write(decimal), hence why we use it here too)
            UIntDecimal converter = new UIntDecimal
            {
                decimalValue = value
            };
            writer.WriteUInt64(converter.longValue1);
            writer.WriteUInt64(converter.longValue2);
        }

        public static void WriteString(this NetworkWriter writer, string value)
        {
            // write 0 for null support, increment real size by 1
            // (note: original HLAPI would write "" for null strings, but if a
            //        string is null on the server then it should also be null
            //        on the client)
            if (value == null)
            {
                writer.WriteUInt16(0);
                return;
            }

            // write string with same method as NetworkReader
            // convert to byte[]
            int size = encoding.GetBytes(value, 0, value.Length, stringBuffer, 0);

            // check if within max size
            if (size >= NetworkWriter.MaxStringLength)
            {
                throw new IndexOutOfRangeException("NetworkWriter.Write(string) too long: " + size + ". Limit: " + NetworkWriter.MaxStringLength);
            }

            // write size and bytes
            writer.WriteUInt16(checked((ushort)(size + 1)));
            writer.WriteBytes(stringBuffer, 0, size);
        }

        // for byte arrays with dynamic size, where the reader doesn't know how many will come
        // (like an inventory with different items etc.)
        public static void WriteBytesAndSize(this NetworkWriter writer, byte[] buffer, int offset, int count)
        {
            // null is supported because [SyncVar]s might be structs with null byte[] arrays
            // write 0 for null array, increment normal size by 1 to save bandwith
            // (using size=-1 for null would limit max size to 32kb instead of 64kb)
            if (buffer == null)
            {
                writer.WritePackedUInt32(0u);
                return;
            }
            writer.WritePackedUInt32(checked((uint)count) + 1u);
            writer.WriteBytes(buffer, offset, count);
        }

        // Weaver needs a write function with just one byte[] parameter
        // (we don't name it .Write(byte[]) because it's really a WriteBytesAndSize since we write size / null info too)
        public static void WriteBytesAndSize(this NetworkWriter writer, byte[] buffer)
        {
            // buffer might be null, so we can't use .Length in that case
            writer.WriteBytesAndSize(buffer, 0, buffer != null ? buffer.Length : 0);
        }

        public static void WriteBytesAndSizeSegment(this NetworkWriter writer, ArraySegment<byte> buffer)
        {
            writer.WriteBytesAndSize(buffer.Array, buffer.Offset, buffer.Count);
        }

        // zigzag encoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public static void WritePackedInt32(this NetworkWriter writer, int i)
        {
            uint zigzagged = (uint)((i >> 31) ^ (i << 1));
            writer.WritePackedUInt32(zigzagged);
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        public static void WritePackedUInt32(this NetworkWriter writer, uint value)
        {
            // for 32 bit values WritePackedUInt64 writes the
            // same exact thing bit by bit
            writer.WritePackedUInt64(value);
        }

        // zigzag encoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public static void WritePackedInt64(this NetworkWriter writer, long i)
        {
            ulong zigzagged = (ulong)((i >> 63) ^ (i << 1));
            writer.WritePackedUInt64(zigzagged);
        }

        public static void WritePackedUInt64(this NetworkWriter writer, ulong value)
        {
            if (value <= 240)
            {
                writer.WriteByte((byte)value);
                return;
            }
            if (value <= 2287)
            {
                writer.WriteByte((byte)(((value - 240) >> 8) + 241));
                writer.WriteByte((byte)(value - 240));
                return;
            }
            if (value <= 67823)
            {
                writer.WriteByte(249);
                writer.WriteByte((byte)((value - 2288) >> 8));
                writer.WriteByte((byte)(value - 2288));
                return;
            }
            if (value <= 16777215)
            {
                writer.WriteByte(250);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                return;
            }
            if (value <= 4294967295)
            {
                writer.WriteByte(251);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                writer.WriteByte((byte)(value >> 24));
                return;
            }
            if (value <= 1099511627775)
            {
                writer.WriteByte(252);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                writer.WriteByte((byte)(value >> 24));
                writer.WriteByte((byte)(value >> 32));
                return;
            }
            if (value <= 281474976710655)
            {
                writer.WriteByte(253);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                writer.WriteByte((byte)(value >> 24));
                writer.WriteByte((byte)(value >> 32));
                writer.WriteByte((byte)(value >> 40));
                return;
            }
            if (value <= 72057594037927935)
            {
                writer.WriteByte(254);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                writer.WriteByte((byte)(value >> 24));
                writer.WriteByte((byte)(value >> 32));
                writer.WriteByte((byte)(value >> 40));
                writer.WriteByte((byte)(value >> 48));
                return;
            }

            // all others
            {
                writer.WriteByte(255);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                writer.WriteByte((byte)(value >> 24));
                writer.WriteByte((byte)(value >> 32));
                writer.WriteByte((byte)(value >> 40));
                writer.WriteByte((byte)(value >> 48));
                writer.WriteByte((byte)(value >> 56));
            }
        }

        public static void WriteVector2(this NetworkWriter writer, Vector2 value)
        {
            writer.WriteSingle(value.x);
            writer.WriteSingle(value.y);
        }

        public static void WriteVector3(this NetworkWriter writer, Vector3 value)
        {
            writer.WriteSingle(value.x);
            writer.WriteSingle(value.y);
            writer.WriteSingle(value.z);
        }

        public static void WriteVector4(this NetworkWriter writer, Vector4 value)
        {
            writer.WriteSingle(value.x);
            writer.WriteSingle(value.y);
            writer.WriteSingle(value.z);
            writer.WriteSingle(value.w);
        }

        public static void WriteVector2Int(this NetworkWriter writer, Vector2Int value)
        {
            writer.WritePackedInt32(value.x);
            writer.WritePackedInt32(value.y);
        }

        public static void WriteVector3Int(this NetworkWriter writer, Vector3Int value)
        {
            writer.WritePackedInt32(value.x);
            writer.WritePackedInt32(value.y);
            writer.WritePackedInt32(value.z);
        }

        public static void WriteColor(this NetworkWriter writer, Color value)
        {
            writer.WriteSingle(value.r);
            writer.WriteSingle(value.g);
            writer.WriteSingle(value.b);
            writer.WriteSingle(value.a);
        }

        public static void WriteColor32(this NetworkWriter writer, Color32 value)
        {
            writer.WriteByte(value.r);
            writer.WriteByte(value.g);
            writer.WriteByte(value.b);
            writer.WriteByte(value.a);
        }

        public static void WriteQuaternion(this NetworkWriter writer, Quaternion value)
        {
            writer.WriteSingle(value.x);
            writer.WriteSingle(value.y);
            writer.WriteSingle(value.z);
            writer.WriteSingle(value.w);
        }

        public static void WriteRect(this NetworkWriter writer, Rect value)
        {
            writer.WriteSingle(value.xMin);
            writer.WriteSingle(value.yMin);
            writer.WriteSingle(value.width);
            writer.WriteSingle(value.height);
        }

        public static void WritePlane(this NetworkWriter writer, Plane value)
        {
            writer.WriteVector3(value.normal);
            writer.WriteSingle(value.distance);
        }

        public static void WriteRay(this NetworkWriter writer, Ray value)
        {
            writer.WriteVector3(value.origin);
            writer.WriteVector3(value.direction);
        }

        public static void WriteMatrix4x4(this NetworkWriter writer, Matrix4x4 value)
        {
            writer.WriteSingle(value.m00);
            writer.WriteSingle(value.m01);
            writer.WriteSingle(value.m02);
            writer.WriteSingle(value.m03);
            writer.WriteSingle(value.m10);
            writer.WriteSingle(value.m11);
            writer.WriteSingle(value.m12);
            writer.WriteSingle(value.m13);
            writer.WriteSingle(value.m20);
            writer.WriteSingle(value.m21);
            writer.WriteSingle(value.m22);
            writer.WriteSingle(value.m23);
            writer.WriteSingle(value.m30);
            writer.WriteSingle(value.m31);
            writer.WriteSingle(value.m32);
            writer.WriteSingle(value.m33);
        }

        public static void WriteGuid(this NetworkWriter writer, Guid value)
        {
            byte[] data = value.ToByteArray();
            writer.WriteBytes(data, 0, data.Length);
        }

        public static void WriteNetworkIdentity(this NetworkWriter writer, NetworkIdentity value)
        {
            if (value == null)
            {
                writer.WritePackedUInt32(0);
                return;
            }
            writer.WritePackedUInt32(value.netId);
        }

        public static void WriteTransform(this NetworkWriter writer, Transform value)
        {
            if (value == null)
            {
                writer.WritePackedUInt32(0);
                return;
            }
            NetworkIdentity identity = value.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                writer.WritePackedUInt32(identity.netId);
            }
            else
            {
                logger.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
                writer.WritePackedUInt32(0);
            }
        }

        public static void WriteGameObject(this NetworkWriter writer, GameObject value)
        {
            if (value == null)
            {
                writer.WritePackedUInt32(0);
                return;
            }
            NetworkIdentity identity = value.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                writer.WritePackedUInt32(identity.netId);
            }
            else
            {
                logger.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
                writer.WritePackedUInt32(0);
            }
        }

        public static void WriteUri(this NetworkWriter writer, Uri uri)
        {
            writer.WriteString(uri.ToString());
        }

        public static void WriteList<T>(this NetworkWriter writer, List<T> list)
        {
            if (list is null)
            {
                writer.WritePackedInt32(-1);
                return;
            }
            writer.WritePackedInt32(list.Count);
            for (int i = 0; i < list.Count; i++)
                writer.Write(list[i]);
        }

        public static void WriteArray<T>(this NetworkWriter writer, T[] array)
        {
            if (array is null)
            {
                writer.WritePackedInt32(-1);
                return;
            }
            writer.WritePackedInt32(array.Length);
            for (int i = 0; i < array.Length; i++)
                writer.Write(array[i]);
        }

        public static void WriteArraySegment<T>(this NetworkWriter writer, ArraySegment<T> segment)
        {
            int length = segment.Count;
            writer.WritePackedInt32(length);
            for (int i = 0; i < length; i++)
            {
                writer.Write(segment.Array[segment.Offset + i]);
            }
        }
    }
}
