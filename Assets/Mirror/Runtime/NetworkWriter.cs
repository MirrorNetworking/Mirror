using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ToArray()
        {
            byte[] data = new byte[Position];
            Array.ConstrainedCopy(buffer, 0, data, 0, Position);
            return data;
        }

        /// <summary>Returns allocation-free ArraySegment until 'Position'.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, Position);
        }

        // WriteBlittable<T> from DOTSNET.
        // this is extremely fast, but only works for blittable types.
        //
        // Benchmark:
        //   WriteQuaternion x 100k, Macbook Pro 2015 @ 2.2Ghz, Unity 2018 LTS (debug mode)
        //
        //                | Median |  Min  |  Max  |  Avg  |  Std  | (ms)
        //     before     |  30.35 | 29.86 | 48.99 | 32.54 |  4.93 |
        //     blittable* |   5.69 |  5.52 | 27.51 |  7.78 |  5.65 |
        //
        //     * without IsBlittable check
        //     => 4-6x faster!
        //
        //   WriteQuaternion x 100k, Macbook Pro 2015 @ 2.2Ghz, Unity 2020.1 (release mode)
        //
        //                | Median |  Min  |  Max  |  Avg  |  Std  | (ms)
        //     before     |   9.41 |  8.90 | 23.02 | 10.72 |  3.07 |
        //     blittable* |   1.48 |  1.40 | 16.03 |  2.60 |  2.71 |
        //
        //     * without IsBlittable check
        //     => 6x faster!
        //
        // Note:
        //   WriteBlittable assumes same endianness for server & client.
        //   All Unity 2018+ platforms are little endian.
        //   => run NetworkWriterTests.BlittableOnThisPlatform() to verify!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteBlittable<T>(T value)
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                Debug.LogError($"{typeof(T)} is not blittable!");
                return;
            }
#endif
            // calculate size
            //   sizeof(T) gets the managed size at compile time.
            //   Marshal.SizeOf<T> gets the unmanaged size at runtime (slow).
            // => our 1mio writes benchmark is 6x slower with Marshal.SizeOf<T>
            // => for blittable types, sizeof(T) is even recommended:
            // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
            int size = sizeof(T);

            // ensure capacity
            EnsureCapacity(Position + size);

            // write blittable
            fixed (byte* ptr = &buffer[Position])
            {
#if UNITY_ANDROID
                // on some android systems, assigning *(T*)ptr throws a NRE if
                // the ptr isn't aligned (i.e. if Position is 1,2,3,5, etc.).
                // here we have to use memcpy.
                //
                // => we can't get a pointer of a struct in C# without
                //    marshalling allocations
                // => instead, we stack allocate an array of type T and use that
                // => stackalloc avoids GC and is very fast. it only works for
                //    value types, but all blittable types are anyway.
                //
                // this way, we can still support blittable reads on android.
                // see also: https://github.com/vis2k/Mirror/issues/3044
                // (solution discovered by AIIO, FakeByte, mischa)
                T* valueBuffer = stackalloc T[1]{value};
                UnsafeUtility.MemCpy(ptr, valueBuffer, size);
#else
                // cast buffer to T* pointer, then assign value to the area
                *(T*)ptr = value;
#endif
            }
            Position += size;
        }

        // blittable'?' template for code reuse
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteBlittableNullable<T>(T? value)
            where T : unmanaged
        {
            // bool isn't blittable. write as byte.
            WriteByte((byte)(value.HasValue ? 0x01 : 0x00));

            // only write value if exists. saves bandwidth.
            if (value.HasValue)
                WriteBlittable(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value) => WriteBlittable(value);

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(Position + count);
            Array.ConstrainedCopy(buffer, offset, this.buffer, Position, count);
            Position += count;
        }

        /// <summary>Writes any type that mirror supports. Uses weaver populated Writer(T).write.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value)
        {
            Action<NetworkWriter, T> writeDelegate = Writer<T>.write;
            if (writeDelegate == null)
            {
                Debug.LogError($"No writer found for {typeof(T)}. This happens either if you are missing a NetworkWriter extension for your custom type, or if weaving failed. Try to reimport a script to weave again.");
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteByte(this NetworkWriter writer, byte value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteByteNullable(this NetworkWriter writer, byte? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSByte(this NetworkWriter writer, sbyte value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSByteNullable(this NetworkWriter writer, sbyte? value) => writer.WriteBlittableNullable(value);

        // char is not blittable. convert to ushort.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteChar(this NetworkWriter writer, char value) => writer.WriteBlittable((ushort)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteCharNullable(this NetworkWriter writer, char? value) => writer.WriteBlittableNullable((ushort?)value);

        // bool is not blittable. convert to byte.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBool(this NetworkWriter writer, bool value) => writer.WriteBlittable((byte)(value ? 1 : 0));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBoolNullable(this NetworkWriter writer, bool? value) => writer.WriteBlittableNullable(value.HasValue ? ((byte)(value.Value ? 1 : 0)) : new byte?());


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteShort(this NetworkWriter writer, short value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteShortNullable(this NetworkWriter writer, short? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUShort(this NetworkWriter writer, ushort value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUShortNullable(this NetworkWriter writer, ushort? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt(this NetworkWriter writer, int value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteIntNullable(this NetworkWriter writer, int? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt(this NetworkWriter writer, uint value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUIntNullable(this NetworkWriter writer, uint? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLong(this NetworkWriter writer, long value)  => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLongNullable(this NetworkWriter writer, long? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteULong(this NetworkWriter writer, ulong value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteULongNullable(this NetworkWriter writer, ulong? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteFloat(this NetworkWriter writer, float value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteFloatNullable(this NetworkWriter writer, float? value) => writer.WriteBlittableNullable(value);

        [StructLayout(LayoutKind.Explicit)]
        internal struct UIntDouble
        {
            [FieldOffset(0)]
            public double doubleValue;

            [FieldOffset(0)]
            public ulong longValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDouble(this NetworkWriter writer, double value)
        {
            // DEBUG: try to find the exact value that fails.
            //UIntDouble convert = new UIntDouble{doubleValue = value};
            //Debug.Log($"=> NetworkWriter.WriteDouble: {value} => 0x{convert.longValue:X8}");


            writer.WriteBlittable(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleNullable(this NetworkWriter writer, double? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDecimal(this NetworkWriter writer, decimal value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDecimalNullable(this NetworkWriter writer, decimal? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                throw new IndexOutOfRangeException($"NetworkWriter.Write(string) too long: {size}. Limit: {NetworkWriter.MaxStringLength}");
            }

            // write size and bytes
            writer.WriteUShort(checked((ushort)(size + 1)));
            writer.WriteBytes(stringBuffer, 0, size);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBytesAndSizeSegment(this NetworkWriter writer, ArraySegment<byte> buffer)
        {
            writer.WriteBytesAndSize(buffer.Array, buffer.Offset, buffer.Count);
        }

        // Weaver needs a write function with just one byte[] parameter
        // (we don't name it .Write(byte[]) because it's really a WriteBytesAndSize since we write size / null info too)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBytesAndSize(this NetworkWriter writer, byte[] buffer)
        {
            // buffer might be null, so we can't use .Length in that case
            writer.WriteBytesAndSize(buffer, 0, buffer != null ? buffer.Length : 0);
        }

        // for byte arrays with dynamic size, where the reader doesn't know how many will come
        // (like an inventory with different items etc.)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteArraySegment<T>(this NetworkWriter writer, ArraySegment<T> segment)
        {
            int length = segment.Count;
            writer.WriteInt(length);
            for (int i = 0; i < length; i++)
            {
                writer.Write(segment.Array[segment.Offset + i]);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector2(this NetworkWriter writer, Vector2 value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector2Nullable(this NetworkWriter writer, Vector2? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector3(this NetworkWriter writer, Vector3 value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector3Nullable(this NetworkWriter writer, Vector3? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector4(this NetworkWriter writer, Vector4 value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector4Nullable(this NetworkWriter writer, Vector4? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector2Int(this NetworkWriter writer, Vector2Int value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector2IntNullable(this NetworkWriter writer, Vector2Int? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector3Int(this NetworkWriter writer, Vector3Int value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVector3IntNullable(this NetworkWriter writer, Vector3Int? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteColor(this NetworkWriter writer, Color value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteColorNullable(this NetworkWriter writer, Color? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteColor32(this NetworkWriter writer, Color32 value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteColor32Nullable(this NetworkWriter writer, Color32? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteQuaternion(this NetworkWriter writer, Quaternion value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteQuaternionNullable(this NetworkWriter writer, Quaternion? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteRect(this NetworkWriter writer, Rect value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteRectNullable(this NetworkWriter writer, Rect? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePlane(this NetworkWriter writer, Plane value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePlaneNullable(this NetworkWriter writer, Plane? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteRay(this NetworkWriter writer, Ray value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteRayNullable(this NetworkWriter writer, Ray? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMatrix4x4(this NetworkWriter writer, Matrix4x4 value) => writer.WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMatrix4x4Nullable(this NetworkWriter writer, Matrix4x4? value) => writer.WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteGuid(this NetworkWriter writer, Guid value)
        {
            byte[] data = value.ToByteArray();
            writer.WriteBytes(data, 0, data.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteGuidNullable(this NetworkWriter writer, Guid? value)
        {
            writer.WriteBool(value.HasValue);
            if (value.HasValue)
                writer.WriteGuid(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteNetworkIdentity(this NetworkWriter writer, NetworkIdentity value)
        {
            if (value == null)
            {
                writer.WriteUInt(0);
                return;
            }
            writer.WriteUInt(value.netId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                Debug.LogWarning($"NetworkWriter {value} has no NetworkIdentity");
                writer.WriteUInt(0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                Debug.LogWarning($"NetworkWriter {value} has no NetworkIdentity");
                writer.WriteUInt(0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUri(this NetworkWriter writer, Uri uri)
        {
            writer.WriteString(uri?.ToString());
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTexture2D(this NetworkWriter writer, Texture2D texture2D)
        {
            writer.Write(texture2D.GetPixels32());
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSprite(this NetworkWriter writer, Sprite sprite)
        {
            writer.WriteTexture2D(sprite.texture);
            writer.WriteRect(sprite.rect);
            writer.WriteVector2(sprite.pivot);
        }
    }
}
