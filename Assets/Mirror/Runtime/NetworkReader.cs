using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Mirror
{
    /// <summary>Helper class that weaver populates with all reader types.</summary>
    // Note that c# creates a different static variable for each type
    // -> Weaver.ReaderWriterProcessor.InitializeReaderAndWriters() populates it
    public static class Reader<T>
    {
        public static Func<NetworkReader, T> read;
    }

    /// <summary>Network Reader for most simple types like floats, ints, buffers, structs, etc. Use NetworkReaderPool.GetReader() to avoid allocations.</summary>
    // Note: This class is intended to be extremely pedantic,
    // and throw exceptions whenever stuff is going slightly wrong.
    // The exceptions will be handled in NetworkServer/NetworkClient.
    public class NetworkReader
    {
        // internal buffer
        // byte[] pointer would work, but we use ArraySegment to also support
        // the ArraySegment constructor
        ArraySegment<byte> buffer;

        /// <summary>Next position to read from the buffer</summary>
        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position;

        /// <summary>Total number of bytes to read from buffer</summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer.Count;
        }

        /// <summary>Remaining bytes that can be read, for convenience.</summary>
        public int Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Length - Position;
        }

        public NetworkReader(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes);
        }

        public NetworkReader(ArraySegment<byte> segment)
        {
            buffer = segment;
        }

        // sometimes it's useful to point a reader on another buffer instead of
        // allocating a new reader (e.g. NetworkReaderPool)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBuffer(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes);
            Position = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBuffer(ArraySegment<byte> segment)
        {
            buffer = segment;
            Position = 0;
        }

        // ReadBlittable<T> from DOTSNET
        // this is extremely fast, but only works for blittable types.
        // => private to make sure nobody accidentally uses it for non-blittable
        //
        // Benchmark: see NetworkWriter.WriteBlittable!
        //
        // Note:
        //   ReadBlittable assumes same endianness for server & client.
        //   All Unity 2018+ platforms are little endian.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe T ReadBlittable<T>()
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                throw new ArgumentException($"{typeof(T)} is not blittable!");
            }
#endif

            // calculate size
            //   sizeof(T) gets the managed size at compile time.
            //   Marshal.SizeOf<T> gets the unmanaged size at runtime (slow).
            // => our 1mio writes benchmark is 6x slower with Marshal.SizeOf<T>
            // => for blittable types, sizeof(T) is even recommended:
            // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
            int size = sizeof(T);

            // enough data to read?
            if (Position + size > buffer.Count)
            {
                throw new EndOfStreamException($"ReadBlittable<{typeof(T)}> out of range: {ToString()}");
            }

            // read blittable
            T value;
            fixed (byte* ptr = &buffer.Array[buffer.Offset + Position])
            {
#if UNITY_ANDROID
                // on some android systems, reading *(T*)ptr throws a NRE if
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
                T* valueBuffer = stackalloc T[1];
                UnsafeUtility.MemCpy(valueBuffer, ptr, size);
                value = valueBuffer[0];
#else
                // cast buffer to a T* pointer and then read from it.
                value = *(T*)ptr;
#endif
            }
            Position += size;
            return value;
        }

        // blittable'?' template for code reuse
        // note: bool isn't blittable. need to read as byte.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T? ReadBlittableNullable<T>()
            where T : unmanaged =>
                ReadByte() != 0 ? ReadBlittable<T>() : default(T?);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte() => ReadBlittable<byte>();

        /// <summary>Read 'count' bytes into the bytes array</summary>
        // TODO why does this also return bytes[]???
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytes(byte[] bytes, int count)
        {
            // check if passed byte array is big enough
            if (count > bytes.Length)
            {
                throw new EndOfStreamException($"ReadBytes can't read {count} + bytes because the passed byte[] only has length {bytes.Length}");
            }

            ArraySegment<byte> data = ReadBytesSegment(count);
            Array.Copy(data.Array, data.Offset, bytes, 0, count);
            return bytes;
        }

        /// <summary>Read 'count' bytes allocation-free as ArraySegment that points to the internal array.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadBytesSegment(int count)
        {
            // check if within buffer limits
            if (Position + count > buffer.Count)
            {
                throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
            }

            // return the segment
            ArraySegment<byte> result = new ArraySegment<byte>(buffer.Array, buffer.Offset + Position, count);
            Position += count;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"NetworkReader pos={Position} len={Length} buffer={BitConverter.ToString(buffer.Array, buffer.Offset, buffer.Count)}";
        }

        /// <summary>Reads any data type that mirror supports. Uses weaver populated Reader(T).read</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>()
        {
            Func<NetworkReader, T> readerDelegate = Reader<T>.read;
            if (readerDelegate == null)
            {
                Debug.LogError($"No reader found for {typeof(T)}. Use a type supported by Mirror or define a custom reader");
                return default;
            }
            return readerDelegate(this);
        }
    }

    // Mirror's Weaver automatically detects all NetworkReader function types,
    // but they do all need to be extensions.
    public static class NetworkReaderExtensions
    {
        // cache encoding instead of creating it each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(this NetworkReader reader) => reader.ReadBlittable<byte>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte? ReadByteNullable(this NetworkReader reader) => reader.ReadBlittableNullable<byte>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ReadSByte(this NetworkReader reader) => reader.ReadBlittable<sbyte>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte? ReadSByteNullable(this NetworkReader reader) => reader.ReadBlittableNullable<sbyte>();

        // bool is not blittable. read as ushort.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ReadChar(this NetworkReader reader) => (char)reader.ReadBlittable<ushort>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char? ReadCharNullable(this NetworkReader reader) => (char?)reader.ReadBlittableNullable<ushort>();

        // bool is not blittable. read as byte.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadBool(this NetworkReader reader) => reader.ReadBlittable<byte>() != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool? ReadBoolNullable(this NetworkReader reader)
        {
            byte? value = reader.ReadBlittableNullable<byte>();
            return value.HasValue ? (value.Value != 0) : default(bool?);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this NetworkReader reader) => (short)reader.ReadUShort();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short? ReadShortNullable(this NetworkReader reader) => reader.ReadBlittableNullable<short>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShort(this NetworkReader reader) => reader.ReadBlittable<ushort>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort? ReadUShortNullable(this NetworkReader reader) => reader.ReadBlittableNullable<ushort>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this NetworkReader reader) => reader.ReadBlittable<int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int? ReadIntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<int>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt(this NetworkReader reader) => reader.ReadBlittable<uint>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint? ReadUIntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<uint>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this NetworkReader reader) => reader.ReadBlittable<long>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long? ReadLongNullable(this NetworkReader reader) => reader.ReadBlittableNullable<long>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadULong(this NetworkReader reader) => reader.ReadBlittable<ulong>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong? ReadULongNullable(this NetworkReader reader) => reader.ReadBlittableNullable<ulong>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloat(this NetworkReader reader) => reader.ReadBlittable<float>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? ReadFloatNullable(this NetworkReader reader) => reader.ReadBlittableNullable<float>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this NetworkReader reader) => reader.ReadBlittable<double>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double? ReadDoubleNullable(this NetworkReader reader) => reader.ReadBlittableNullable<double>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal ReadDecimal(this NetworkReader reader) => reader.ReadBlittable<decimal>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal? ReadDecimalNullable(this NetworkReader reader) => reader.ReadBlittableNullable<decimal>();

        /// <exception cref="T:System.ArgumentException">if an invalid utf8 string is sent</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadString(this NetworkReader reader)
        {
            // read number of bytes
            ushort size = reader.ReadUShort();

            // null support, see NetworkWriter
            if (size == 0)
                return null;

            int realSize = size - 1;

            // make sure it's within limits to avoid allocation attacks etc.
            if (realSize >= NetworkWriter.MaxStringLength)
            {
                throw new EndOfStreamException($"ReadString too long: {realSize}. Limit is: {NetworkWriter.MaxStringLength}");
            }

            ArraySegment<byte> data = reader.ReadBytesSegment(realSize);

            // convert directly from buffer to string via encoding
            return encoding.GetString(data.Array, data.Offset, data.Count);
        }

        /// <exception cref="T:OverflowException">if count is invalid</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ReadBytesAndSize(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count -1 is the length of the array
            uint count = reader.ReadUInt();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? null : reader.ReadBytes(checked((int)(count - 1u)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ReadBytes(this NetworkReader reader, int count)
        {
            byte[] bytes = new byte[count];
            reader.ReadBytes(bytes, count);
            return bytes;
        }

        /// <exception cref="T:OverflowException">if count is invalid</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArraySegment<byte> ReadBytesAndSizeSegment(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count - 1 is the length of the array
            uint count = reader.ReadUInt();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? default : reader.ReadBytesSegment(checked((int)(count - 1u)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ReadVector2(this NetworkReader reader) => reader.ReadBlittable<Vector2>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2? ReadVector2Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector2>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ReadVector3(this NetworkReader reader) => reader.ReadBlittable<Vector3>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3? ReadVector3Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector3>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ReadVector4(this NetworkReader reader) => reader.ReadBlittable<Vector4>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4? ReadVector4Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector4>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int ReadVector2Int(this NetworkReader reader) => reader.ReadBlittable<Vector2Int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int? ReadVector2IntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector2Int>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Int ReadVector3Int(this NetworkReader reader) => reader.ReadBlittable<Vector3Int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Int? ReadVector3IntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector3Int>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ReadColor(this NetworkReader reader) => reader.ReadBlittable<Color>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color? ReadColorNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Color>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 ReadColor32(this NetworkReader reader) => reader.ReadBlittable<Color32>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32? ReadColor32Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Color32>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion ReadQuaternion(this NetworkReader reader) => reader.ReadBlittable<Quaternion>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion? ReadQuaternionNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Quaternion>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ReadRect(this NetworkReader reader) => reader.ReadBlittable<Rect>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect? ReadRectNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Rect>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane ReadPlane(this NetworkReader reader) => reader.ReadBlittable<Plane>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane? ReadPlaneNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Plane>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Ray ReadRay(this NetworkReader reader) => reader.ReadBlittable<Ray>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Ray? ReadRayNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Ray>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 ReadMatrix4x4(this NetworkReader reader)=> reader.ReadBlittable<Matrix4x4>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4? ReadMatrix4x4Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Matrix4x4>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this NetworkReader reader) => new Guid(reader.ReadBytes(16));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid? ReadGuidNullable(this NetworkReader reader) => reader.ReadBool() ? ReadGuid(reader) : default(Guid?);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkIdentity ReadNetworkIdentity(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            if (netId == 0)
                return null;

            // look in server spawned
            if (NetworkServer.active &&
                NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity serverIdentity))
                return serverIdentity;

            // look in client spawned
            if (NetworkClient.active &&
                NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity clientIdentity))
                return clientIdentity;

            // a netId not being in spawned is common.
            // for example, "[SyncVar] NetworkIdentity target" netId would not
            // be known on client if the monster walks out of proximity for a
            // moment. no need to log any error or warning here.
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkBehaviour ReadNetworkBehaviour(this NetworkReader reader)
        {
            // reuse ReadNetworkIdentity, get the component at index
            NetworkIdentity identity = ReadNetworkIdentity(reader);

            // a netId not being in spawned is common.
            // for example, "[SyncVar] NetworkBehaviour target" netId would not
            // be known on client if the monster walks out of proximity for a
            // moment. no need to log any error or warning here.
            if (identity == null)
                return null;

            // if identity isn't null, then index is also sent to read before returning
            byte componentIndex = reader.ReadByte();
            return identity.NetworkBehaviours[componentIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadNetworkBehaviour<T>(this NetworkReader reader) where T : NetworkBehaviour
        {
            return reader.ReadNetworkBehaviour() as T;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkBehaviour.NetworkBehaviourSyncVar ReadNetworkBehaviourSyncVar(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            byte componentIndex = default;

            // if netId is not 0, then index is also sent to read before returning
            if (netId != 0)
            {
                componentIndex = reader.ReadByte();
            }

            return new NetworkBehaviour.NetworkBehaviourSyncVar(netId, componentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Transform ReadTransform(this NetworkReader reader)
        {
            // Don't use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.transform : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject ReadGameObject(this NetworkReader reader)
        {
            // Don't use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.gameObject : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> ReadList<T>(this NetworkReader reader)
        {
            int length = reader.ReadInt();
            if (length < 0)
                return null;
            List<T> result = new List<T>(length);
            for (int i = 0; i < length; i++)
            {
                result.Add(reader.Read<T>());
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] ReadArray<T>(this NetworkReader reader)
        {
            int length = reader.ReadInt();

            //  we write -1 for null
            if (length < 0)
                return null;

            // todo throw an exception for other negative values (we never write them, likely to be attacker)

            // this assumes that a reader for T reads at least 1 bytes
            // we can't know the exact size of T because it could have a user created reader
            // NOTE: don't add to length as it could overflow if value is int.max
            if (length > reader.Length - reader.Position)
            {
                throw new EndOfStreamException($"Received array that is too large: {length}");
            }

            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = reader.Read<T>();
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Uri ReadUri(this NetworkReader reader)
        {
            string uriString = reader.ReadString();
            return (string.IsNullOrWhiteSpace(uriString) ? null : new Uri(uriString));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Texture2D ReadTexture2D(this NetworkReader reader)
        {
            Texture2D texture2D = new Texture2D(32, 32);
            texture2D.SetPixels32(reader.Read<Color32[]>());
            texture2D.Apply();
            return texture2D;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Sprite ReadSprite(this NetworkReader reader)
        {
            return Sprite.Create(reader.ReadTexture2D(), reader.ReadRect(), reader.ReadVector2());
        }
    }
}
