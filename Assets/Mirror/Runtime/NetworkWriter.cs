using System;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
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

        // fixed size buffer so we can pool it and don't need runtime resizing.
        readonly byte[] buffer;

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position;

        // helper field to calculate space in bytes remaining to write
        public int Space => buffer.Length - Position;

        // totol capacity independent of position
        public int Capacity => buffer.Length;

        // create new writer with size
        public NetworkWriter(int size)
        {
            buffer = new byte[size];
        }

        // ArraySegment pointing to internal data, considering position
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, Position);
        }

        // WriteBlittable<T> from DOTSNET.
        // this is extremely fast, but only works for blittable types.
        public unsafe bool WriteBlittable<T>(T value)
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                Debug.LogError(typeof(T) + " is not blittable!");
                return false;
            }
#endif
            // calculate size
            int size = sizeof(T);

            // enough space in buffer?
            if (Space >= size)
            {
                fixed (byte* ptr = &buffer[Position])
                {
                    // cast buffer to T* pointer, then assign value to the area
                    *(T*)ptr = value;
                }
                Position += size;
                return true;
            }

            // not enough space to write
            return false;
        }

        // WriteBytes from DOTSNET
        public unsafe void WriteBytes(byte[] bytes, int offset, int count)
        {
            // enough space in buffer?
            // and anything to write?
            if (Space >= count &&
                bytes != null && count > 0)
            {
                // write 'count' bytes at position

                // 10 mio writes: 868ms
                //Array.Copy(value.Array, value.Offset, buffer, Position, value.Count);

                // 10 mio writes: 775ms
                //Buffer.BlockCopy(value.Array, value.Offset, buffer, Position, value.Count);

                fixed (byte* dst = &buffer[Position],
                             src = &bytes[offset])
                {
                    // 10 mio writes: 637ms
                    UnsafeUtility.MemCpy(dst, src, count);
                }

                // update position
                Position += count;
            }
        }

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
        // cache encoding instead of creating it with BinaryWriter each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        static readonly byte[] stringBuffer = new byte[NetworkWriter.MaxStringLength];

        public static void WriteByte(this NetworkWriter writer, byte value) => writer.WriteBlittable(value);
        public static void WriteSByte(this NetworkWriter writer, sbyte value) => writer.WriteBlittable(value);
        public static void WriteChar(this NetworkWriter writer, char value) => writer.WriteBlittable((short)value); // char isn't blittable
        public static void WriteBoolean(this NetworkWriter writer, bool value) => writer.WriteBlittable((byte)(value ? 1 : 0));
        public static void WriteUInt16(this NetworkWriter writer, ushort value) => writer.WriteBlittable(value);
        public static void WriteInt16(this NetworkWriter writer, short value) => writer.WriteUInt16((ushort)value);
        public static void WriteUInt32(this NetworkWriter writer, uint value) => writer.WriteBlittable(value);
        public static void WriteInt32(this NetworkWriter writer, int value) => writer.WriteBlittable(value);
        public static void WriteUInt64(this NetworkWriter writer, ulong value) => writer.WriteBlittable(value);
        public static void WriteInt64(this NetworkWriter writer, long value) => writer.WriteBlittable(value);
        public static void WriteSingle(this NetworkWriter writer, float value) => writer.WriteBlittable(value);
        public static void WriteDouble(this NetworkWriter writer, double value) => writer.WriteBlittable(value);
        public static void WriteDecimal(this NetworkWriter writer, decimal value) => writer.WriteBlittable(value);

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
                writer.WriteUInt32(0u);
                return;
            }
            writer.WriteUInt32(checked((uint)count) + 1u);
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

        public static void WriteVector2(this NetworkWriter writer, Vector2 value) => writer.WriteBlittable(value);
        public static void WriteVector3(this NetworkWriter writer, Vector3 value) => writer.WriteBlittable(value);
        public static void WriteVector4(this NetworkWriter writer, Vector4 value) => writer.WriteBlittable(value);
        public static void WriteVector2Int(this NetworkWriter writer, Vector2Int value) => writer.WriteBlittable(value);
        public static void WriteVector3Int(this NetworkWriter writer, Vector3Int value) => writer.WriteBlittable(value);
        public static void WriteColor(this NetworkWriter writer, Color value) => writer.WriteBlittable(value);
        public static void WriteColor32(this NetworkWriter writer, Color32 value) => writer.WriteBlittable(value);
        public static void WriteQuaternion(this NetworkWriter writer, Quaternion value) => writer.WriteBlittable(value);
        public static void WriteRect(this NetworkWriter writer, Rect value) => writer.WriteBlittable(value);
        public static void WritePlane(this NetworkWriter writer, Plane value) => writer.WriteBlittable(value);
        public static void WriteRay(this NetworkWriter writer, Ray value) => writer.WriteBlittable(value);
        public static void WriteMatrix4x4(this NetworkWriter writer, Matrix4x4 value) => writer.WriteBlittable(value);

        public static void WriteGuid(this NetworkWriter writer, Guid value)
        {
            byte[] data = value.ToByteArray();
            writer.WriteBytes(data, 0, data.Length);
        }

        public static void WriteNetworkIdentity(this NetworkWriter writer, NetworkIdentity value)
        {
            if (value == null)
            {
                writer.WriteUInt32(0);
                return;
            }
            writer.WriteUInt32(value.netId);
        }
    }
}
