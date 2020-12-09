using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// a class that holds readers for the different types
    /// Note that c# creates a different static variable for each
    /// type
    /// This will be populated by the weaver
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class Reader<T>
    {
        public static Func<NetworkReader, T> read;
    }

    /// <summary>
    /// Binary stream Reader. Supports simple types, buffers, arrays, structs, and nested types
    /// <para>Use <see cref="NetworkReaderPool.GetReader">NetworkReaderPool.GetReader</see> to reduce memory allocation</para>
    /// <para>
    /// Note: This class is intended to be extremely pedantic,
    /// and throw exceptions whenever stuff is going slightly wrong.
    /// The exceptions will be handled in NetworkServer/NetworkClient.
    /// </para>
    /// </summary>
    public class NetworkReader
    {
        static readonly ILogger logger = LogFactory.GetLogger<NetworkReader>();

        // Custom NetworkReader that doesn't use C#'s built in MemoryStream in order to
        // avoid allocations.
        //
        // Benchmark: 100kb byte[] passed to NetworkReader constructor 1000x
        //   before with MemoryStream
        //     0.8% CPU time, 250KB memory, 3.82ms
        //   now:
        //     0.0% CPU time,  32KB memory, 0.02ms

        // internal buffer
        // byte[] pointer would work, but we use ArraySegment to also support
        // the ArraySegment constructor
        internal ArraySegment<byte> buffer;

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        /// <summary>
        /// Next position to read from the buffer
        /// </summary>
        public int Position;

        /// <summary>
        /// Total number of bytes to read from buffer
        /// </summary>
        public int Length => buffer.Count;

        public NetworkReader(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes);
        }

        public NetworkReader(ArraySegment<byte> segment)
        {
            buffer = segment;
        }

        // ReadBlittable<T> from DOTSNET
        // Benchmark: see NetworkWriter.WriteBlittable!
        /// <summary>
        /// Read blittable type from buffer
        /// <para>
        ///     this is extremely fast, but only works for blittable types.
        /// </para>
        /// <para>
        ///     Note:
        ///     ReadBlittable assumes same endianness for server and client.
        ///     All Unity 2018+ platforms are little endian.
        /// </para>
        /// </summary>
        /// <remarks>
        ///     See <see href="https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types">Blittable and Non-Blittable Types</see>
        ///     for more info.
        /// </remarks>
        /// <typeparam name="T">Needs to be unmanaged, see <see href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types">unmanaged types</see></typeparam>
        /// <returns></returns>
        public unsafe T ReadBlittable<T>()
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                throw new ArgumentException(typeof(T) + " is not blittable!");
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
                // cast buffer to a T* pointer and then read from it.
                value = *(T*)ptr;
            }
            Position += size;
            return value;
        }

        /// <summary>
        /// read bytes into <paramref name="bytes"/>
        /// </summary>
        /// <returns><paramref name="bytes"/></returns>
        public byte[] ReadBytes(byte[] bytes, int count)
        {
            // check if passed byte array is big enough
            if (count > bytes.Length)
            {
                throw new EndOfStreamException("ReadBytes can't read " + count + " + bytes because the passed byte[] only has length " + bytes.Length);
            }

            ArraySegment<byte> data = ReadBytesSegment(count);
            Array.Copy(data.Array, data.Offset, bytes, 0, count);
            return bytes;
        }

        /// <summary>
        /// Create Segment from current position
        /// <para>
        ///     Useful to parse payloads etc. without allocating
        /// </para>
        /// </summary>
        public ArraySegment<byte> ReadBytesSegment(int count)
        {
            // check if within buffer limits
            if (Position + count > buffer.Count)
            {
                throw new EndOfStreamException("ReadBytesSegment can't read " + count + " bytes because it would read past the end of the stream. " + ToString());
            }

            // return the segment
            ArraySegment<byte> result = new ArraySegment<byte>(buffer.Array, buffer.Offset + Position, count);
            Position += count;
            return result;
        }

        /// <returns>Information about reader: pos, len, buffer contents</returns>
        public override string ToString()
        {
            return $"NetworkReader pos={Position} len={Length} buffer={BitConverter.ToString(buffer.Array, buffer.Offset, buffer.Count)}";
        }

        /// <summary>
        /// Reads any data type that mirror supports
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Read<T>()
        {
            Func<NetworkReader, T> readerDelegate = Reader<T>.read;
            if (readerDelegate == null)
            {
                logger.LogError($"No reader found for {typeof(T)}. Use a type supported by Mirror or define a custom reader");
                return default;
            }
            return readerDelegate(this);
        }
    }

    /// <summary>
    /// Built in Reader functions for Mirror
    /// <para>
    ///     Weaver automatically decects all extension methods for NetworkWriter 
    /// </para>
    /// </summary>
    public static class NetworkReaderExtensions
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkReaderExtensions));

        // cache encoding instead of creating it each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);

        public static byte ReadByte(this NetworkReader reader) => reader.ReadBlittable<byte>();
        public static sbyte ReadSByte(this NetworkReader reader) => reader.ReadBlittable<sbyte>();
        public static char ReadChar(this NetworkReader reader) => (char)reader.ReadBlittable<short>(); // char isn't blittable
        public static bool ReadBoolean(this NetworkReader reader) => reader.ReadBlittable<byte>() != 0; // bool isn't blittable
        public static short ReadInt16(this NetworkReader reader) => reader.ReadBlittable<short>();
        public static ushort ReadUInt16(this NetworkReader reader) => reader.ReadBlittable<ushort>();
        public static int ReadInt32(this NetworkReader reader) => reader.ReadBlittable<int>();
        public static uint ReadUInt32(this NetworkReader reader) => reader.ReadBlittable<uint>();
        public static long ReadInt64(this NetworkReader reader) => reader.ReadBlittable<long>();
        public static ulong ReadUInt64(this NetworkReader reader) => reader.ReadBlittable<ulong>();
        public static float ReadSingle(this NetworkReader reader) => reader.ReadBlittable<float>();
        public static double ReadDouble(this NetworkReader reader) => reader.ReadBlittable<double>();
        public static decimal ReadDecimal(this NetworkReader reader) => reader.ReadBlittable<decimal>();

        /// <exception cref="T:System.ArgumentException">if an invalid utf8 string is sent</exception>
        public static string ReadString(this NetworkReader reader)
        {
            // read number of bytes
            ushort size = reader.ReadUInt16();

            // null support, see NetworkWriter
            if (size == 0)
                return null;

            int realSize = size - 1;

            // make sure it's within limits to avoid allocation attacks etc.
            if (realSize >= NetworkWriter.MaxStringLength)
            {
                throw new EndOfStreamException("ReadString too long: " + realSize + ". Limit is: " + NetworkWriter.MaxStringLength);
            }

            ArraySegment<byte> data = reader.ReadBytesSegment(realSize);

            // convert directly from buffer to string via encoding
            return encoding.GetString(data.Array, data.Offset, data.Count);
        }

        /// <exception cref="T:OverflowException">if count is invalid</exception>
        public static byte[] ReadBytesAndSize(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count -1 is the length of the array
            uint count = reader.ReadUInt32();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? null : reader.ReadBytes(checked((int)(count - 1u)));
        }

        /// <exception cref="T:OverflowException">if count is invalid</exception>
        public static ArraySegment<byte> ReadBytesAndSizeSegment(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count - 1 is the length of the array
            uint count = reader.ReadUInt32();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? default : reader.ReadBytesSegment(checked((int)(count - 1u)));
        }

        public static Vector2 ReadVector2(this NetworkReader reader) => reader.ReadBlittable<Vector2>();
        public static Vector3 ReadVector3(this NetworkReader reader) => reader.ReadBlittable<Vector3>();
        public static Vector4 ReadVector4(this NetworkReader reader) => reader.ReadBlittable<Vector4>();
        public static Vector2Int ReadVector2Int(this NetworkReader reader) => reader.ReadBlittable<Vector2Int>();
        public static Vector3Int ReadVector3Int(this NetworkReader reader) => reader.ReadBlittable<Vector3Int>();
        public static Color ReadColor(this NetworkReader reader) => reader.ReadBlittable<Color>();
        public static Color32 ReadColor32(this NetworkReader reader) => reader.ReadBlittable<Color32>();
        public static Quaternion ReadQuaternion(this NetworkReader reader) => reader.ReadBlittable<Quaternion>();
        public static Rect ReadRect(this NetworkReader reader) => reader.ReadBlittable<Rect>();
        public static Plane ReadPlane(this NetworkReader reader) => reader.ReadBlittable<Plane>();
        public static Ray ReadRay(this NetworkReader reader) => reader.ReadBlittable<Ray>();
        public static Matrix4x4 ReadMatrix4x4(this NetworkReader reader) => reader.ReadBlittable<Matrix4x4>();

        public static byte[] ReadBytes(this NetworkReader reader, int count)
        {
            byte[] bytes = new byte[count];
            reader.ReadBytes(bytes, count);
            return bytes;
        }

        public static Guid ReadGuid(this NetworkReader reader) => new Guid(reader.ReadBytes(16));
        public static Transform ReadTransform(this NetworkReader reader)
        {
            // Dont use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.transform : null;
        }

        public static GameObject ReadGameObject(this NetworkReader reader)
        {
            // Dont use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.gameObject : null;
        }

        public static NetworkIdentity ReadNetworkIdentity(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt32();
            if (netId == 0)
                return null;

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                return identity;
            }

            if (logger.WarnEnabled()) logger.LogFormat(LogType.Warning, "ReadNetworkIdentity netId:{0} not found in spawned", netId);
            return null;
        }

        public static NetworkBehaviour ReadNetworkBehaviour(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt32();
            if (netId == 0)
                return null;

            // if netId is not 0, then index is also sent to read before returning
            byte componentIndex = reader.ReadByte();

            if (!NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                if (logger.WarnEnabled()) logger.LogFormat(LogType.Warning, "ReadNetworkBehaviour netId:{0} not found in spawned", netId);
                return null;
            }

            return identity.NetworkBehaviours[componentIndex];
        }

        public static T ReadNetworkBehaviour<T>(this NetworkReader reader) where T : NetworkBehaviour
        {
            return reader.ReadNetworkBehaviour() as T;
        }

        public static NetworkBehaviour.NetworkBehaviourSyncVar ReadNetworkBehaviourSyncVar(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt32();
            byte componentIndex = default;

            // if netId is not 0, then index is also sent to read before returning
            if (netId != 0)
            {
                componentIndex = reader.ReadByte();
            }

            return new NetworkBehaviour.NetworkBehaviourSyncVar(netId, componentIndex);
        }

        public static List<T> ReadList<T>(this NetworkReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0)
                return null;
            List<T> result = new List<T>(length);
            for (int i = 0; i < length; i++)
            {
                result.Add(reader.Read<T>());
            }
            return result;
        }

        public static T[] ReadArray<T>(this NetworkReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0)
                return null;
            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = reader.Read<T>();
            }
            return result;
        }

        public static Uri ReadUri(this NetworkReader reader)
        {
            return new Uri(reader.ReadString());
        }
    }
}
