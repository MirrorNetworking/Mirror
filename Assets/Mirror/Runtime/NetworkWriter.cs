using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Mirror
{
    /// <summary>Helper class that weaver populates with all writer types.</summary>
    // Note that c# creates a different static variable for each type
    // -> Weaver.ReaderWriterProcessor.InitializeReaderAndWriters() populates it
    public static class Writer<T>
    {
        public static Action<NetworkWriter, T> write;
    }

    /// <summary>Network Writer for most simple types like floats, ints, buffers, structs, etc. Use NetworkWriterPool.GetReader() to avoid allocations.</summary>
    public class NetworkWriter
    {
        public const int MaxStringLength = 1024 * 32;

        // create writer immediately with it's own buffer so no one can mess with it and so that we can resize it.
        // note: BinaryWriter allocates too much, so we only use a MemoryStream
        // => 1500 bytes by default because on average, most packets will be <= MTU
        byte[] buffer = new byte[1500];

        /// <summary>Next position to write to the buffer</summary>
        public int Position;

        /// <summary>Reset both the position and length of the stream</summary>
        // Leaves the capacity the same so that we can reuse this writer without
        // extra allocations
        public void Reset()
        {
            Position = 0;
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

        /// <summary>Copies buffer until 'Position' to a new array.</summary>
        public byte[] ToArray()
        {
            byte[] data = new byte[Position];
            Array.ConstrainedCopy(buffer, 0, data, 0, Position);
            return data;
        }

        /// <summary>Returns allocation-free ArraySegment until 'Position'.</summary>
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, Position);
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(Position + 1);
            buffer[Position++] = value;
        }

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(Position + count);
            Array.ConstrainedCopy(buffer, offset, this.buffer, Position, count);
            Position += count;
        }

        /// <summary>Writes any type that mirror supports. Uses weaver populated Writer(T).write.</summary>
        public void Write<T>(T value)
        {
            Action<NetworkWriter, T> writeDelegate = Writer<T>.write;
            if (writeDelegate == null)
            {
                Debug.LogError($"No writer found for {typeof(T)}. Use a type supported by Mirror or define a custom writer");
            }
            else
            {
                writeDelegate(this, value);
            }
        }
    }

    // Mirror's Weaver automatically detects all NetworkWriter function types,
    // but they do all need to be extensions.
    public static class NetworkWriterExtensions
    {
        // cache encoding instead of creating it with BinaryWriter each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        static readonly byte[] stringBuffer = new byte[NetworkWriter.MaxStringLength];

        public static void WriteByte(this NetworkWriter writer, byte value) => writer.WriteByte(value);

        public static void WriteSByte(this NetworkWriter writer, sbyte value) => writer.WriteByte((byte)value);

        public static void WriteChar(this NetworkWriter writer, char value) => writer.WriteUShort(value);

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteBool instead.")]
        public static void WriteBoolean(this NetworkWriter writer, bool value) => writer.WriteBool(value);
        public static void WriteBool(this NetworkWriter writer, bool value) => writer.WriteByte((byte)(value ? 1 : 0));

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteUShort instead.")]
        public static void WriteUInt16(this NetworkWriter writer, ushort value) => writer.WriteUShort(value);
        public static void WriteUShort(this NetworkWriter writer, ushort value)
        {
            writer.WriteByte((byte)value);
            writer.WriteByte((byte)(value >> 8));
        }

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteShort instead.")]
        public static void WriteInt16(this NetworkWriter writer, short value) => writer.WriteShort(value);
        public static void WriteShort(this NetworkWriter writer, short value) => writer.WriteUShort((ushort)value);

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteUInt instead.")]
        public static void WriteUInt32(this NetworkWriter writer, uint value) => writer.WriteUInt(value);
        public static void WriteUInt(this NetworkWriter writer, uint value)
        {
            writer.WriteByte((byte)value);
            writer.WriteByte((byte)(value >> 8));
            writer.WriteByte((byte)(value >> 16));
            writer.WriteByte((byte)(value >> 24));
        }

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteInt instead.")]
        public static void WriteInt32(this NetworkWriter writer, int value) => writer.WriteInt(value);
        public static void WriteInt(this NetworkWriter writer, int value) => writer.WriteUInt((uint)value);

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteULong instead.")]
        public static void WriteUInt64(this NetworkWriter writer, ulong value) => writer.WriteULong(value);
        public static void WriteULong(this NetworkWriter writer, ulong value)
        {
            writer.WriteByte((byte)value);
            writer.WriteByte((byte)(value >> 8));
            writer.WriteByte((byte)(value >> 16));
            writer.WriteByte((byte)(value >> 24));
            writer.WriteByte((byte)(value >> 32));
            writer.WriteByte((byte)(value >> 40));
            writer.WriteByte((byte)(value >> 48));
            writer.WriteByte((byte)(value >> 56));
        }

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteLong instead.")]
        public static void WriteInt64(this NetworkWriter writer, long value) => writer.WriteLong(value);
        public static void WriteLong(this NetworkWriter writer, long value) => writer.WriteULong((ulong)value);

        // Deprecated 2021-05-18
        [Obsolete("We've cleaned up the API. Use WriteFloat instead.")]
        public static void WriteSingle(this NetworkWriter writer, float value) => writer.WriteFloat(value);
        public static void WriteFloat(this NetworkWriter writer, float value)
        {
            UIntFloat converter = new UIntFloat
            {
                floatValue = value
            };
            writer.WriteUInt(converter.intValue);
        }

        public static void WriteDouble(this NetworkWriter writer, double value)
        {
            UIntDouble converter = new UIntDouble
            {
                doubleValue = value
            };
            writer.WriteULong(converter.longValue);
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
            writer.WriteULong(converter.longValue1);
            writer.WriteULong(converter.longValue2);
        }

        public static void WriteString(this NetworkWriter writer, string value)
        {
            // write 0 for null support, increment real size by 1
            // (note: original HLAPI would write "" for null strings, but if a
            //        string is null on the server then it should also be null
            //        on the client)
            if (value == null)
            {
                writer.WriteUShort(0);
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
            writer.WriteUShort(checked((ushort)(size + 1)));
            writer.WriteBytes(stringBuffer, 0, size);
        }

        // for byte arrays with dynamic size, where the reader doesn't know how many will come
        // (like an inventory with different items etc.)
        public static void WriteBytesAndSize(this NetworkWriter writer, byte[] buffer, int offset, int count)
        {
            // null is supported because [SyncVar]s might be structs with null byte[] arrays
            // write 0 for null array, increment normal size by 1 to save bandwidth
            // (using size=-1 for null would limit max size to 32kb instead of 64kb)
            if (buffer == null)
            {
                writer.WriteUInt(0u);
                return;
            }
            writer.WriteUInt(checked((uint)count) + 1u);
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

        public static void WriteVector2(this NetworkWriter writer, Vector2 value)
        {
            writer.WriteFloat(value.x);
            writer.WriteFloat(value.y);
        }

        public static void WriteVector3(this NetworkWriter writer, Vector3 value)
        {
            writer.WriteFloat(value.x);
            writer.WriteFloat(value.y);
            writer.WriteFloat(value.z);
        }

        // TODO add nullable support to weaver instead
        public static void WriteVector3Nullable(this NetworkWriter writer, Vector3? value)
        {
            writer.WriteBool(value.HasValue);
            if (value.HasValue)
                writer.WriteVector3(value.Value);
        }

        public static void WriteVector4(this NetworkWriter writer, Vector4 value)
        {
            writer.WriteFloat(value.x);
            writer.WriteFloat(value.y);
            writer.WriteFloat(value.z);
            writer.WriteFloat(value.w);
        }

        public static void WriteVector2Int(this NetworkWriter writer, Vector2Int value)
        {
            writer.WriteInt(value.x);
            writer.WriteInt(value.y);
        }

        public static void WriteVector3Int(this NetworkWriter writer, Vector3Int value)
        {
            writer.WriteInt(value.x);
            writer.WriteInt(value.y);
            writer.WriteInt(value.z);
        }

        public static void WriteColor(this NetworkWriter writer, Color value)
        {
            writer.WriteFloat(value.r);
            writer.WriteFloat(value.g);
            writer.WriteFloat(value.b);
            writer.WriteFloat(value.a);
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
            writer.WriteFloat(value.x);
            writer.WriteFloat(value.y);
            writer.WriteFloat(value.z);
            writer.WriteFloat(value.w);
        }

        // TODO add nullable support to weaver instead
        public static void WriteQuaternionNullable(this NetworkWriter writer, Quaternion? value)
        {
            writer.WriteBool(value.HasValue);
            if (value.HasValue)
                writer.WriteQuaternion(value.Value);
        }

        public static void WriteRect(this NetworkWriter writer, Rect value)
        {
            writer.WriteFloat(value.xMin);
            writer.WriteFloat(value.yMin);
            writer.WriteFloat(value.width);
            writer.WriteFloat(value.height);
        }

        public static void WritePlane(this NetworkWriter writer, Plane value)
        {
            writer.WriteVector3(value.normal);
            writer.WriteFloat(value.distance);
        }

        public static void WriteRay(this NetworkWriter writer, Ray value)
        {
            writer.WriteVector3(value.origin);
            writer.WriteVector3(value.direction);
        }

        public static void WriteMatrix4x4(this NetworkWriter writer, Matrix4x4 value)
        {
            writer.WriteFloat(value.m00);
            writer.WriteFloat(value.m01);
            writer.WriteFloat(value.m02);
            writer.WriteFloat(value.m03);
            writer.WriteFloat(value.m10);
            writer.WriteFloat(value.m11);
            writer.WriteFloat(value.m12);
            writer.WriteFloat(value.m13);
            writer.WriteFloat(value.m20);
            writer.WriteFloat(value.m21);
            writer.WriteFloat(value.m22);
            writer.WriteFloat(value.m23);
            writer.WriteFloat(value.m30);
            writer.WriteFloat(value.m31);
            writer.WriteFloat(value.m32);
            writer.WriteFloat(value.m33);
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
                writer.WriteUInt(0);
                return;
            }
            writer.WriteUInt(value.netId);
        }

        public static void WriteNetworkBehaviour(this NetworkWriter writer, NetworkBehaviour value)
        {
            if (value == null)
            {
                writer.WriteUInt(0);
                return;
            }
            writer.WriteUInt(value.netId);
            writer.WriteByte((byte)value.ComponentIndex);
        }

        public static void WriteTransform(this NetworkWriter writer, Transform value)
        {
            if (value == null)
            {
                writer.WriteUInt(0);
                return;
            }
            NetworkIdentity identity = value.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                writer.WriteUInt(identity.netId);
            }
            else
            {
                Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
                writer.WriteUInt(0);
            }
        }

        public static void WriteGameObject(this NetworkWriter writer, GameObject value)
        {
            if (value == null)
            {
                writer.WriteUInt(0);
                return;
            }
            NetworkIdentity identity = value.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                writer.WriteUInt(identity.netId);
            }
            else
            {
                Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
                writer.WriteUInt(0);
            }
        }

        public static void WriteUri(this NetworkWriter writer, Uri uri)
        {
            writer.WriteString(uri?.ToString());
        }

        public static void WriteList<T>(this NetworkWriter writer, List<T> list)
        {
            if (list is null)
            {
                writer.WriteInt(-1);
                return;
            }
            writer.WriteInt(list.Count);
            for (int i = 0; i < list.Count; i++)
                writer.Write(list[i]);
        }

        public static void WriteArray<T>(this NetworkWriter writer, T[] array)
        {
            if (array is null)
            {
                writer.WriteInt(-1);
                return;
            }
            writer.WriteInt(array.Length);
            for (int i = 0; i < array.Length; i++)
                writer.Write(array[i]);
        }

        public static void WriteArraySegment<T>(this NetworkWriter writer, ArraySegment<T> segment)
        {
            int length = segment.Count;
            writer.WriteInt(length);
            for (int i = 0; i < length; i++)
            {
                writer.Write(segment.Array[segment.Offset + i]);
            }
        }
    }
}
