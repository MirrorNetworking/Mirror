using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        public int Length => buffer.Count;

        /// <summary>Remaining bytes that can be read, for convenience.</summary>
        public int Remaining => Length - Position;

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
        public void SetBuffer(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes);
            Position = 0;
        }

        public void SetBuffer(ArraySegment<byte> segment)
        {
            buffer = segment;
            Position = 0;
        }

        // IMPORTANT: ReadBlittable<T> via fixed pinning WON'T WORK on android:
        //            https://github.com/vis2k/Mirror/issues/3044
        //            if we ever do it again, use NativeArray + .GetPtr()!
        public byte ReadByte()
        {
            if (Position + 1 > buffer.Count)
            {
                throw new EndOfStreamException($"ReadByte out of range:{ToString()}");
            }
            return buffer.Array[buffer.Offset + Position++];
        }

        /// <summary>Read 'count' bytes into the bytes array</summary>
        // TODO why does this also return bytes[]???
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

        public override string ToString()
        {
            return $"NetworkReader pos={Position} len={Length} buffer={BitConverter.ToString(buffer.Array, buffer.Offset, buffer.Count)}";
        }

        /// <summary>Reads any data type that mirror supports. Uses weaver populated Reader(T).read</summary>
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

        public static byte ReadByte(this NetworkReader reader) => reader.ReadByte();
        public static byte? ReadByteNullable(this NetworkReader reader) => reader.ReadBool() ? ReadByte(reader) : default(byte?);

        public static sbyte ReadSByte(this NetworkReader reader) => (sbyte)reader.ReadByte();
        public static sbyte? ReadSByteNullable(this NetworkReader reader) => reader.ReadBool() ? ReadSByte(reader) : default(sbyte?);

        public static char ReadChar(this NetworkReader reader) => (char)reader.ReadUShort();
        public static char? ReadCharNullable(this NetworkReader reader) => reader.ReadBool() ? ReadChar(reader) : default(char?);

        public static bool ReadBool(this NetworkReader reader) => reader.ReadByte() != 0;
        public static bool? ReadBoolNullable(this NetworkReader reader) => reader.ReadBool() ? ReadBool(reader) : default(bool?);

        public static short ReadShort(this NetworkReader reader) => (short)reader.ReadUShort();
        public static short? ReadShortNullable(this NetworkReader reader) => reader.ReadBool() ? ReadShort(reader) : default(short?);

        public static ushort ReadUShort(this NetworkReader reader)
        {
            ushort value = 0;
            value |= reader.ReadByte();
            value |= (ushort)(reader.ReadByte() << 8);
            return value;
        }
        public static ushort? ReadUShortNullable(this NetworkReader reader) => reader.ReadBool() ? ReadUShort(reader) : default(ushort?);

        public static int ReadInt(this NetworkReader reader) => (int)reader.ReadUInt();
        public static int? ReadIntNullable(this NetworkReader reader) => reader.ReadBool() ? ReadInt(reader) : default(int?);

        public static uint ReadUInt(this NetworkReader reader)
        {
            uint value = 0;
            value |= reader.ReadByte();
            value |= (uint)(reader.ReadByte() << 8);
            value |= (uint)(reader.ReadByte() << 16);
            value |= (uint)(reader.ReadByte() << 24);
            return value;
        }
        public static uint? ReadUIntNullable(this NetworkReader reader) => reader.ReadBool() ? ReadUInt(reader) : default(uint?);

        public static long ReadLong(this NetworkReader reader) => (long)reader.ReadULong();
        public static long? ReadLongNullable(this NetworkReader reader) => reader.ReadBool() ? ReadLong(reader) : default(long?);

        public static ulong ReadULong(this NetworkReader reader)
        {
            ulong value = 0;
            value |= reader.ReadByte();
            value |= ((ulong)reader.ReadByte()) << 8;
            value |= ((ulong)reader.ReadByte()) << 16;
            value |= ((ulong)reader.ReadByte()) << 24;
            value |= ((ulong)reader.ReadByte()) << 32;
            value |= ((ulong)reader.ReadByte()) << 40;
            value |= ((ulong)reader.ReadByte()) << 48;
            value |= ((ulong)reader.ReadByte()) << 56;
            return value;
        }
        public static ulong? ReadULongNullable(this NetworkReader reader) => reader.ReadBool() ? ReadULong(reader) : default(ulong?);

        public static float ReadFloat(this NetworkReader reader)
        {
            UIntFloat converter = new UIntFloat();
            converter.intValue = reader.ReadUInt();
            return converter.floatValue;
        }
        public static float? ReadFloatNullable(this NetworkReader reader) => reader.ReadBool() ? ReadFloat(reader) : default(float?);

        public static double ReadDouble(this NetworkReader reader)
        {
            UIntDouble converter = new UIntDouble();
            converter.longValue = reader.ReadULong();
            return converter.doubleValue;
        }
        public static double? ReadDoubleNullable(this NetworkReader reader) => reader.ReadBool() ? ReadDouble(reader) : default(double?);

        public static decimal ReadDecimal(this NetworkReader reader)
        {
            UIntDecimal converter = new UIntDecimal();
            converter.longValue1 = reader.ReadULong();
            converter.longValue2 = reader.ReadULong();
            return converter.decimalValue;
        }
        public static decimal? ReadDecimalNullable(this NetworkReader reader) => reader.ReadBool() ? ReadDecimal(reader) : default(decimal?);

        /// <exception cref="T:System.ArgumentException">if an invalid utf8 string is sent</exception>
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
        public static byte[] ReadBytesAndSize(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count -1 is the length of the array
            uint count = reader.ReadUInt();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? null : reader.ReadBytes(checked((int)(count - 1u)));
        }

        public static byte[] ReadBytes(this NetworkReader reader, int count)
        {
            byte[] bytes = new byte[count];
            reader.ReadBytes(bytes, count);
            return bytes;
        }

        /// <exception cref="T:OverflowException">if count is invalid</exception>
        public static ArraySegment<byte> ReadBytesAndSizeSegment(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count - 1 is the length of the array
            uint count = reader.ReadUInt();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? default : reader.ReadBytesSegment(checked((int)(count - 1u)));
        }

        public static Vector2 ReadVector2(this NetworkReader reader) => new Vector2(reader.ReadFloat(), reader.ReadFloat());
        public static Vector2? ReadVector2Nullable(this NetworkReader reader) => reader.ReadBool() ? ReadVector2(reader) : default(Vector2?);

        public static Vector3 ReadVector3(this NetworkReader reader) => new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        public static Vector3? ReadVector3Nullable(this NetworkReader reader) => reader.ReadBool() ? ReadVector3(reader) : default(Vector3?);

        public static Vector4 ReadVector4(this NetworkReader reader) => new Vector4(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        public static Vector4? ReadVector4Nullable(this NetworkReader reader) => reader.ReadBool() ? ReadVector4(reader) : default(Vector4?);

        public static Vector2Int ReadVector2Int(this NetworkReader reader) => new Vector2Int(reader.ReadInt(), reader.ReadInt());
        public static Vector2Int? ReadVector2IntNullable(this NetworkReader reader) => reader.ReadBool() ? ReadVector2Int(reader) : default(Vector2Int?);

        public static Vector3Int ReadVector3Int(this NetworkReader reader) => new Vector3Int(reader.ReadInt(), reader.ReadInt(), reader.ReadInt());
        public static Vector3Int? ReadVector3IntNullable(this NetworkReader reader) => reader.ReadBool() ? ReadVector3Int(reader) : default(Vector3Int?);

        public static Color ReadColor(this NetworkReader reader) => new Color(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        public static Color? ReadColorNullable(this NetworkReader reader) => reader.ReadBool() ? new Color(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()) : default(Color?);

        public static Color32 ReadColor32(this NetworkReader reader) => new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        public static Color32? ReadColor32Nullable(this NetworkReader reader) => reader.ReadBool() ? new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) : default(Color32?);

        public static Quaternion ReadQuaternion(this NetworkReader reader) => new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        public static Quaternion? ReadQuaternionNullable(this NetworkReader reader) => reader.ReadBool() ? ReadQuaternion(reader) : default(Quaternion?);

        public static Rect ReadRect(this NetworkReader reader) => new Rect(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        public static Rect? ReadRectNullable(this NetworkReader reader) => reader.ReadBool() ? ReadRect(reader) : default(Rect?);

        public static Plane ReadPlane(this NetworkReader reader) => new Plane(reader.ReadVector3(), reader.ReadFloat());
        public static Plane? ReadPlaneNullable(this NetworkReader reader) => reader.ReadBool() ? ReadPlane(reader) : default(Plane?);

        public static Ray ReadRay(this NetworkReader reader) => new Ray(reader.ReadVector3(), reader.ReadVector3());
        public static Ray? ReadRayNullable(this NetworkReader reader) => reader.ReadBool() ? ReadRay(reader) : default(Ray?);

        public static Matrix4x4 ReadMatrix4x4(this NetworkReader reader)
        {
            return new Matrix4x4
            {
                m00 = reader.ReadFloat(),
                m01 = reader.ReadFloat(),
                m02 = reader.ReadFloat(),
                m03 = reader.ReadFloat(),
                m10 = reader.ReadFloat(),
                m11 = reader.ReadFloat(),
                m12 = reader.ReadFloat(),
                m13 = reader.ReadFloat(),
                m20 = reader.ReadFloat(),
                m21 = reader.ReadFloat(),
                m22 = reader.ReadFloat(),
                m23 = reader.ReadFloat(),
                m30 = reader.ReadFloat(),
                m31 = reader.ReadFloat(),
                m32 = reader.ReadFloat(),
                m33 = reader.ReadFloat()
            };
        }
        public static Matrix4x4? ReadMatrix4x4Nullable(this NetworkReader reader) => reader.ReadBool() ? ReadMatrix4x4(reader) : default(Matrix4x4?);

        public static Guid ReadGuid(this NetworkReader reader) => new Guid(reader.ReadBytes(16));
        public static Guid? ReadGuidNullable(this NetworkReader reader) => reader.ReadBool() ? ReadGuid(reader) : default(Guid?);

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

        public static T ReadNetworkBehaviour<T>(this NetworkReader reader) where T : NetworkBehaviour
        {
            return reader.ReadNetworkBehaviour() as T;
        }

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

        public static Transform ReadTransform(this NetworkReader reader)
        {
            // Don't use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.transform : null;
        }

        public static GameObject ReadGameObject(this NetworkReader reader)
        {
            // Don't use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.gameObject : null;
        }

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

        public static Uri ReadUri(this NetworkReader reader)
        {
            string uriString = reader.ReadString();
            return (string.IsNullOrWhiteSpace(uriString) ? null : new Uri(uriString));
        }

        public static Texture2D ReadTexture2D(this NetworkReader reader)
        {
            Texture2D texture2D = new Texture2D(32, 32);
            texture2D.SetPixels32(reader.Read<Color32[]>());
            texture2D.Apply();
            return texture2D;
        }

        public static Sprite ReadSprite(this NetworkReader reader)
        {
            return Sprite.Create(reader.ReadTexture2D(), reader.ReadRect(), reader.ReadVector2());
        }
    }
}
