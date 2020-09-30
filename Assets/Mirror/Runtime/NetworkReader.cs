// Custom NetworkReader that doesn't use C#'s built in MemoryStream in order to
// avoid allocations.
//
// Benchmark: 100kb byte[] passed to NetworkReader constructor 1000x
//   before with MemoryStream
//     0.8% CPU time, 250KB memory, 3.82ms
//   now:
//     0.0% CPU time,  32KB memory, 0.02ms
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // Note: This class is intended to be extremely pedantic, and
    // throw exceptions whenever stuff is going slightly wrong.
    // The exceptions will be handled in NetworkServer/NetworkClient.
    /// <summary>
    /// Binary stream Reader. Supports simple types, buffers, arrays, structs, and nested types
    /// <para>Use <see cref="NetworkReaderPool.GetReader">NetworkReaderPool.GetReader</see> to reduce memory allocation</para>
    /// </summary>
    public class NetworkReader
    {
        // internal buffer
        // byte[] pointer would work, but we use ArraySegment to also support
        // the ArraySegment constructor
        internal ArraySegment<byte> buffer;

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position;
        public int Length => buffer.Count;

        public NetworkReader(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes);
        }

        public NetworkReader(ArraySegment<byte> segment)
        {
            buffer = segment;
        }

        public byte ReadByte()
        {
            if (Position + 1 > buffer.Count)
            {
                throw new EndOfStreamException("ReadByte out of range:" + ToString());
            }
            return buffer.Array[buffer.Offset + Position++];
        }
        public int ReadInt32() => (int)ReadUInt32();
        public uint ReadUInt32()
        {
            uint value = 0;
            value |= ReadByte();
            value |= (uint)(ReadByte() << 8);
            value |= (uint)(ReadByte() << 16);
            value |= (uint)(ReadByte() << 24);
            return value;
        }
        public long ReadInt64() => (long)ReadUInt64();
        public ulong ReadUInt64()
        {
            ulong value = 0;
            value |= ReadByte();
            value |= ((ulong)ReadByte()) << 8;
            value |= ((ulong)ReadByte()) << 16;
            value |= ((ulong)ReadByte()) << 24;
            value |= ((ulong)ReadByte()) << 32;
            value |= ((ulong)ReadByte()) << 40;
            value |= ((ulong)ReadByte()) << 48;
            value |= ((ulong)ReadByte()) << 56;
            return value;
        }

        // read bytes into the passed buffer
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

        // useful to parse payloads etc. without allocating
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

        public override string ToString()
        {
            return "NetworkReader pos=" + Position + " len=" + Length + " buffer=" + BitConverter.ToString(buffer.Array, buffer.Offset, buffer.Count);
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
        public static sbyte ReadSByte(this NetworkReader reader) => (sbyte)reader.ReadByte();
        public static char ReadChar(this NetworkReader reader) => (char)reader.ReadUInt16();
        public static bool ReadBoolean(this NetworkReader reader) => reader.ReadByte() != 0;
        public static short ReadInt16(this NetworkReader reader) => (short)reader.ReadUInt16();
        public static ushort ReadUInt16(this NetworkReader reader)
        {
            ushort value = 0;
            value |= reader.ReadByte();
            value |= (ushort)(reader.ReadByte() << 8);
            return value;
        }
        public static int ReadInt32(this NetworkReader reader) => (int)reader.ReadUInt32();
        public static uint ReadUInt32(this NetworkReader reader)
        {
            uint value = 0;
            value |= reader.ReadByte();
            value |= (uint)(reader.ReadByte() << 8);
            value |= (uint)(reader.ReadByte() << 16);
            value |= (uint)(reader.ReadByte() << 24);
            return value;
        }
        public static long ReadInt64(this NetworkReader reader) => (long)reader.ReadUInt64();
        public static ulong ReadUInt64(this NetworkReader reader)
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
        public static float ReadSingle(this NetworkReader reader)
        {
            UIntFloat converter = new UIntFloat();
            converter.intValue = reader.ReadUInt32();
            return converter.floatValue;
        }
        public static double ReadDouble(this NetworkReader reader)
        {
            UIntDouble converter = new UIntDouble();
            converter.longValue = reader.ReadUInt64();
            return converter.doubleValue;
        }
        public static decimal ReadDecimal(this NetworkReader reader)
        {
            UIntDecimal converter = new UIntDecimal();
            converter.longValue1 = reader.ReadUInt64();
            converter.longValue2 = reader.ReadUInt64();
            return converter.decimalValue;
        }

        // note: this will throw an ArgumentException if an invalid utf8 string is sent
        // null support, see NetworkWriter
        public static string ReadString(this NetworkReader reader)
        {
            // read number of bytes
            ushort size = reader.ReadUInt16();

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

        // Use checked() to force it to throw OverflowException if data is invalid
        // null support, see NetworkWriter
        public static byte[] ReadBytesAndSize(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count -1 is the length of the array
            uint count = reader.ReadUInt32();
            return count == 0 ? null : reader.ReadBytes(checked((int)(count - 1u)));
        }

        public static ArraySegment<byte> ReadBytesAndSizeSegment(this NetworkReader reader)
        {
            // count = 0 means the array was null
            // otherwise count - 1 is the length of the array
            uint count = reader.ReadUInt32();
            return count == 0 ? default : reader.ReadBytesSegment(checked((int)(count - 1u)));
        }

        // TODO this is still used by weaver
        [Obsolete("Use ReadInt32 instead")]
        public static int ReadPackedInt32(this NetworkReader reader)
        {
            return reader.ReadInt32();
        }

        // TODO this is still used by weaver
        [Obsolete("Use ReadUInt32 instead")]
        public static uint ReadPackedUInt32(this NetworkReader reader)
        {
            return reader.ReadUInt32();
        }

        // TODO this is still used by weaver
        [Obsolete("Use ReadInt64 instead")]
        public static long ReadPackedInt64(this NetworkReader reader)
        {
            return reader.ReadInt64();
        }

        // TODO this is still used by weaver
        [Obsolete("Use ReadUInt64 instead")]
        public static ulong ReadPackedUInt64(this NetworkReader reader)
        {
            return reader.ReadUInt64();
        }

        public static Vector2 ReadVector2(this NetworkReader reader) => new Vector2(reader.ReadSingle(), reader.ReadSingle());
        public static Vector3 ReadVector3(this NetworkReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Vector4 ReadVector4(this NetworkReader reader) => new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Vector2Int ReadVector2Int(this NetworkReader reader) => new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
        public static Vector3Int ReadVector3Int(this NetworkReader reader) => new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        public static Color ReadColor(this NetworkReader reader) => new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Color32 ReadColor32(this NetworkReader reader) => new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        public static Quaternion ReadQuaternion(this NetworkReader reader) => new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Rect ReadRect(this NetworkReader reader) => new Rect(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Plane ReadPlane(this NetworkReader reader) => new Plane(reader.ReadVector3(), reader.ReadSingle());
        public static Ray ReadRay(this NetworkReader reader) => new Ray(reader.ReadVector3(), reader.ReadVector3());

        public static Matrix4x4 ReadMatrix4x4(this NetworkReader reader)
        {
            return new Matrix4x4
            {
                m00 = reader.ReadSingle(),
                m01 = reader.ReadSingle(),
                m02 = reader.ReadSingle(),
                m03 = reader.ReadSingle(),
                m10 = reader.ReadSingle(),
                m11 = reader.ReadSingle(),
                m12 = reader.ReadSingle(),
                m13 = reader.ReadSingle(),
                m20 = reader.ReadSingle(),
                m21 = reader.ReadSingle(),
                m22 = reader.ReadSingle(),
                m23 = reader.ReadSingle(),
                m30 = reader.ReadSingle(),
                m31 = reader.ReadSingle(),
                m32 = reader.ReadSingle(),
                m33 = reader.ReadSingle()
            };
        }

        public static byte[] ReadBytes(this NetworkReader reader, int count)
        {
            byte[] bytes = new byte[count];
            reader.ReadBytes(bytes, count);
            return bytes;
        }

        public static Guid ReadGuid(this NetworkReader reader) => new Guid(reader.ReadBytes(16));

        public static NetworkIdentity ReadNetworkIdentity(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt32();
            if (netId == 0)
                return null;

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                return identity;
            }

            // logger.LogFormat(LogType.Warning, "ReadNetworkIdentity netId:{0} not found in spawned", netId);
            return null;
        }

        public static void ReadMessage<T>(this NetworkReader reader, T msg) where T : NetworkMessage
        {
            msg.Deserialize(reader);
        }
    }
}
