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
    public class NetworkReader
    {
        // internal buffer
        // byte[] pointer would work, but we use ArraySegment to also support
        // the ArraySegment constructor
        ArraySegment<byte> buffer;

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position;
        public int Length => buffer.Count;

        // cache encoding instead of creating it each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);

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
        public sbyte ReadSByte() => (sbyte)ReadByte();
        // read char the same way that NetworkWriter writes it (2 bytes)
        public char ReadChar() => (char)ReadUInt16();
        public bool ReadBoolean() => ReadByte() != 0;
        public short ReadInt16() => (short)ReadUInt16();
        public ushort ReadUInt16()
        {
            ushort value = 0;
            value |= ReadByte();
            value |= (ushort)(ReadByte() << 8);
            return value;
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
        public float ReadSingle()
        {
            UIntFloat converter = new UIntFloat();
            converter.intValue = ReadUInt32();
            return converter.floatValue;
        }
        public double ReadDouble()
        {
            UIntDouble converter = new UIntDouble();
            converter.longValue = ReadUInt64();
            return converter.doubleValue;
        }
        public decimal ReadDecimal()
        {
            UIntDecimal converter = new UIntDecimal();
            converter.longValue1 = ReadUInt64();
            converter.longValue2 = ReadUInt64();
            return converter.decimalValue;
        }

        // note: this will throw an ArgumentException if an invalid utf8 string is sent
        // null support, see NetworkWriter
        public string ReadString()
        {
            // read number of bytes
            ushort size = ReadUInt16();

            if (size == 0)
                return null;
            
            int realSize = size - 1;

            // make sure it's within limits to avoid allocation attacks etc.
            if (realSize >= NetworkWriter.MaxStringLength)
            {
                throw new EndOfStreamException("ReadString too long: " + realSize + ". Limit is: " + NetworkWriter.MaxStringLength);
            }

            // check if within buffer limits
            if (Position + realSize > buffer.Count)
            {
                throw new EndOfStreamException("ReadString can't read " + realSize + " bytes because it would read past the end of the stream. " + ToString());
            }

            // convert directly from buffer to string via encoding
            string result = encoding.GetString(buffer.Array, buffer.Offset + Position, realSize);
            Position += realSize;
            return result;
        }

        // read bytes into the passed buffer
        public byte[] ReadBytes(byte[] bytes, int count)
        {
            // check if passed byte array is big enough
            if (count > bytes.Length)
            {
                throw new EndOfStreamException("ReadBytes can't read " + count + " + bytes because the passed byte[] only has length " + bytes.Length);
            }

            // check if within buffer limits
            if (Position + count > buffer.Count)
            {
                throw new EndOfStreamException("ReadBytes can't read " + count + " bytes because it would read past the end of the stream. " + ToString());
            }

            // copy it directly from the array
            Array.Copy(buffer.Array, buffer.Offset + Position, bytes, 0, count);
            Position += count;
            return bytes;
        }

        public byte[] ReadBytes(int count)
        {
            byte[] bytes = new byte[count];
            ReadBytes(bytes, count);
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

        // Use checked() to force it to throw OverflowException if data is invalid
        // null support, see NetworkWriter
        public byte[] ReadBytesAndSize()
        {
            // count = 0 means the array was null
            // otherwise count -1 is the length of the array 
            uint count = ReadPackedUInt32();
            return count == 0 ? null : ReadBytes(checked((int)(count - 1u)));
        }

        public ArraySegment<byte> ReadBytesAndSizeSegment()
        {

            // count = 0 means the array was null
            // otherwise count - 1 is the length of the array
            uint count = ReadPackedUInt32();
            return count == 0 ? default : ReadBytesSegment(checked((int)(count - 1u)));
        }

        // zigzag decoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public int ReadPackedInt32()
        {
            uint data = ReadPackedUInt32();
            return (int)((data >> 1) ^ -(data & 1));
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        // NOTE: big endian.
        // Use checked() to force it to throw OverflowException if data is invalid
        public uint ReadPackedUInt32() => checked((uint)ReadPackedUInt64());

        // zigzag decoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public long ReadPackedInt64()
        {
            ulong data = ReadPackedUInt64();
            return ((long)(data >> 1)) ^ -((long)data & 1);
        }

        public ulong ReadPackedUInt64()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + ((a0 - (ulong)241) << 8) + a1;
            }

            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return 2288 + ((ulong)a1 << 8) + a2;
            }

            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16);
            }

            byte a4 = ReadByte();
            if (a0 == 251)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24);
            }

            byte a5 = ReadByte();
            if (a0 == 252)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32);
            }

            byte a6 = ReadByte();
            if (a0 == 253)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40);
            }

            byte a7 = ReadByte();
            if (a0 == 254)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48);
            }

            byte a8 = ReadByte();
            if (a0 == 255)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48)  + (((ulong)a8) << 56);
            }

            throw new IndexOutOfRangeException("ReadPackedUInt64() failure: " + a0);
        }

        public Vector2 ReadVector2() => new Vector2(ReadSingle(), ReadSingle());
        public Vector3 ReadVector3() => new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        public Vector4 ReadVector4() => new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        public Vector2Int ReadVector2Int() => new Vector2Int(ReadPackedInt32(), ReadPackedInt32());
        public Vector3Int ReadVector3Int() => new Vector3Int(ReadPackedInt32(), ReadPackedInt32(), ReadPackedInt32());
        public Color ReadColor() => new Color(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        public Color32 ReadColor32() => new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
        public Quaternion ReadQuaternion() => new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        public Rect ReadRect() => new Rect(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        public Plane ReadPlane() => new Plane(ReadVector3(), ReadSingle());
        public Ray ReadRay() => new Ray(ReadVector3(), ReadVector3());

        public Matrix4x4 ReadMatrix4x4()
        {
            return new Matrix4x4
            {
                m00 = ReadSingle(),
                m01 = ReadSingle(),
                m02 = ReadSingle(),
                m03 = ReadSingle(),
                m10 = ReadSingle(),
                m11 = ReadSingle(),
                m12 = ReadSingle(),
                m13 = ReadSingle(),
                m20 = ReadSingle(),
                m21 = ReadSingle(),
                m22 = ReadSingle(),
                m23 = ReadSingle(),
                m30 = ReadSingle(),
                m31 = ReadSingle(),
                m32 = ReadSingle(),
                m33 = ReadSingle()
            };
        }

        public Guid ReadGuid() => new Guid(ReadBytes(16));
        public Transform ReadTransform() => ReadNetworkIdentity()?.transform;
        public GameObject ReadGameObject() => ReadNetworkIdentity()?.gameObject;

        public NetworkIdentity ReadNetworkIdentity()
        {
            uint netId = ReadPackedUInt32();
            if (netId == 0) return null;

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                return identity;
            }

            if (LogFilter.Debug) Debug.Log("ReadNetworkIdentity netId:" + netId + " not found in spawned");
            return null;
        }

        public override string ToString()
        {
            return "NetworkReader pos=" + Position + " len=" + Length + " buffer=" + BitConverter.ToString(buffer.Array, buffer.Offset, buffer.Count);
        }
    }
}
