using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // Binary stream Writer. Supports simple types, buffers, arrays, structs, and nested types
    public class NetworkWriter
    {
        // cache encoding instead of creating it with BinaryWriter each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        public const int MaxStringLength = 1024 * 32;
        static readonly byte[] stringBuffer = new byte[MaxStringLength];

        // create writer immediately with it's own buffer so no one can mess with it and so that we can resize it.
        // note: BinaryWriter allocates too much, so we only use a MemoryStream
        readonly MemoryStream stream = new MemoryStream();

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position { get { return (int)stream.Position; } set { stream.Position = value; } }

        // MemoryStream has 3 values: Position, Length and Capacity.
        // Position is used to indicate where we are writing
        // Length is how much data we have written
        // capacity is how much memory we have allocated
        // ToArray returns all the data we have written,  regardless of the current position
        public byte[] ToArray()
        {
            stream.Flush();
            return stream.ToArray();
        }

        // Gets the serialized data in an ArraySegment<byte>
        // this is similar to ToArray(),  but it gets the data in O(1)
        // and without allocations.
        // Do not write anything else or modify the NetworkWriter
        // while you are using the ArraySegment
        public ArraySegment<byte> ToArraySegment()
        {
            stream.Flush();
            if (stream.TryGetBuffer(out ArraySegment<byte> data))
            { 
                return data;
            }
            throw new Exception("Cannot expose contents of memory stream. Make sure that MemoryStream buffer is publicly visible (see MemoryStream source code).");
        }

        // reset both the position and length of the stream,  but leaves the capacity the same
        // so that we can reuse this writer without extra allocations
        public void SetLength(long value)
        {
            stream.SetLength(value);
        }

        [Obsolete("Use WriteUInt16 instead")]
        public void Write(ushort value) => WriteUInt16(value);

        public void WriteUInt16(ushort value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)(value >> 8));
        }

        [Obsolete("Use WriteUInt32 instead")]
        public void Write(uint value) => WriteUInt32(value);

        public void WriteUInt32(uint value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 8) & 0xFF));
            WriteByte((byte)((value >> 16) & 0xFF));
            WriteByte((byte)((value >> 24) & 0xFF));
        }

        [Obsolete("Use WriteUInt64 instead")]
        public void Write(ulong value) => WriteUInt64(value);        

        public void WriteUInt64(ulong value) {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 8) & 0xFF));
            WriteByte((byte)((value >> 16) & 0xFF));
            WriteByte((byte)((value >> 24) & 0xFF));
            WriteByte((byte)((value >> 32) & 0xFF));
            WriteByte((byte)((value >> 40) & 0xFF));
            WriteByte((byte)((value >> 48) & 0xFF));
            WriteByte((byte)((value >> 56) & 0xFF));
        }

        [Obsolete("Use WriteByte instead")]
        public void Write(byte value) => stream.WriteByte(value);

        public void WriteByte(byte value) => stream.WriteByte(value);

        [Obsolete("Use WriteSByte instead")]
        public void Write(sbyte value) => WriteByte((byte)value);

        public void WriteSByte(sbyte value) => WriteByte((byte)value);

        // write char the same way that NetworkReader reads it (2 bytes)
        [Obsolete("Use WriteChar instead")]
        public void Write(char value) => WriteUInt16((ushort)value);

        public void WriteChar(char value) => WriteUInt16((ushort)value);

        [Obsolete("Use WriteBoolean instead")]
        public void Write(bool value) => WriteByte((byte)(value ? 1 : 0));

        public void WriteBoolean(bool value) => WriteByte((byte)(value ? 1 : 0));

        [Obsolete("Use WriteInt16 instead")]
        public void Write(short value) => WriteUInt16((ushort)value);

        public void WriteInt16(short value) => WriteUInt16((ushort)value);

        [Obsolete("Use WriteInt32 instead")]
        public void Write(int value) => WriteUInt32((uint)value);

        public void WriteInt32(int value) => WriteUInt32((uint)value);

        [Obsolete("Use WriteInt64 instead")]
        public void Write(long value) => WriteUInt64((ulong)value);

        public void WriteInt64(long value) => WriteUInt64((ulong)value);

        [Obsolete("Use WriteSingle instead")]
        public void Write(float value) => WriteSingle(value);
        
        public void WriteSingle(float value) {
            UIntFloat converter = new UIntFloat
            {
                floatValue = value
            };
            WriteUInt32(converter.intValue);
        }

        [Obsolete("Use WriteDouble instead")]
        public void Write(double value) => WriteDouble(value);

        public void WriteDouble(double value)
        {
            UIntDouble converter = new UIntDouble
            {
                doubleValue = value
            };
            WriteUInt64(converter.longValue);
        }

        [Obsolete("Use WriteDecimal instead")]
        public void Write(decimal value) => WriteDecimal(value);

        public void WriteDecimal(decimal value)
        {
            // the only way to read it without allocations is to both read and
            // write it with the FloatConverter (which is not binary compatible
            // to writer.Write(decimal), hence why we use it here too)
            UIntDecimal converter = new UIntDecimal
            {
                decimalValue = value
            };
            WriteUInt64(converter.longValue1);
            WriteUInt64(converter.longValue2);
        }

        [Obsolete("Use WriteString instead")]
        public void Write(string value) => WriteString(value);

        public void WriteString(string value)
        {
            // write 0 for null support, increment real size by 1
            // (note: original HLAPI would write "" for null strings, but if a
            //        string is null on the server then it should also be null
            //        on the client)
            if (value == null)
            {
                WriteUInt16((ushort)0);
                return;
            }

            // write string with same method as NetworkReader
            // convert to byte[]
            int size = encoding.GetBytes(value, 0, value.Length, stringBuffer, 0);

            // check if within max size
            if (size >= MaxStringLength)
            {
                throw new IndexOutOfRangeException("NetworkWriter.Write(string) too long: " + size + ". Limit: " + MaxStringLength);
            }

            // write size and bytes
            WriteUInt16(checked((ushort)(size + 1)));
            WriteBytes(stringBuffer, 0, size);
        }

        [Obsolete("Use WriteBytes instead")]
        public void Write(byte[] buffer, int offset, int count) => WriteBytes(buffer, offset, count);

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            // no null check because we would need to write size info for that too (hence WriteBytesAndSize)
            stream.Write(buffer, offset, count);
        }

        // for byte arrays with dynamic size, where the reader doesn't know how many will come
        // (like an inventory with different items etc.)
        public void WriteBytesAndSize(byte[] buffer, int offset, int count)
        {
            // null is supported because [SyncVar]s might be structs with null byte[] arrays
            // write 0 for null array, increment normal size by 1 to save bandwith
            // (using size=-1 for null would limit max size to 32kb instead of 64kb)
            if (buffer == null)
            {
                WritePackedUInt32(0u);
                return;
            }
            WritePackedUInt32(checked((uint)count) + 1u);
            WriteBytes(buffer, offset, count);
        }

        // Weaver needs a write function with just one byte[] parameter
        // (we don't name it .Write(byte[]) because it's really a WriteBytesAndSize since we write size / null info too)
        public void WriteBytesAndSize(byte[] buffer)
        {
            // buffer might be null, so we can't use .Length in that case
            WriteBytesAndSize(buffer, 0, buffer != null ? buffer.Length : 0);
        }

        public void WriteBytesAndSizeSegment(ArraySegment<byte> buffer)
        {
            WriteBytesAndSize(buffer.Array, buffer.Offset, buffer.Count);
        }

        // zigzag encoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public void WritePackedInt32(int i)
        {
            uint zigzagged = (uint)((i >> 31) ^ (i << 1));
            WritePackedUInt32(zigzagged);
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        public void WritePackedUInt32(uint value)
        {
            // for 32 bit values WritePackedUInt64 writes the
            // same exact thing bit by bit
            WritePackedUInt64(value);
        }

        // zigzag encoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        public void WritePackedInt64(long i)
        {
            ulong zigzagged = (ulong)((i >> 63) ^ (i << 1));
            WritePackedUInt64(zigzagged);
        }

        public void WritePackedUInt64(ulong value)
        {
            if (value <= 240)
            {
                WriteByte((byte)value);
                return;
            }
            if (value <= 2287)
            {
                WriteByte((byte)(((value - 240) >> 8) + 241));
                WriteByte((byte)((value - 240) & 0xFF));
                return;
            }
            if (value <= 67823)
            {
                WriteByte((byte)249);
                WriteByte((byte)((value - 2288) >> 8));
                WriteByte((byte)((value - 2288) & 0xFF));
                return;
            }
            if (value <= 16777215)
            {
                WriteByte((byte)250);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                WriteByte((byte)251);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                WriteByte((byte)252);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                WriteByte((byte)253);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                WriteByte((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                WriteByte((byte)254);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                WriteByte((byte)((value >> 40) & 0xFF));
                WriteByte((byte)((value >> 48) & 0xFF));
                return;
            }

            // all others
            {
                WriteByte((byte)255);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                WriteByte((byte)((value >> 40) & 0xFF));
                WriteByte((byte)((value >> 48) & 0xFF));
                WriteByte((byte)((value >> 56) & 0xFF));
            }
        }

        [Obsolete("Use WriteVector2 instead")]
        public void Write(Vector2 value) => WriteVector2(value);

        public void WriteVector2(Vector2 value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
        }

        [Obsolete("Use WriteVector3 instead")]
        public void Write(Vector3 value) => WriteVector3(value);

        public void WriteVector3(Vector3 value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
            WriteSingle(value.z);
        }

        [Obsolete("Use WriteVector4 instead")]
        public void Write(Vector4 value) => WriteVector4(value);

        public void WriteVector4(Vector4 value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
            WriteSingle(value.z);
            WriteSingle(value.w);
        }

        [Obsolete("Use WriteVector2Int instead")]
        public void Write(Vector2Int value) => WriteVector2Int(value);

        public void WriteVector2Int(Vector2Int value)
        {
            WritePackedInt32(value.x);
            WritePackedInt32(value.y);
        }

        [Obsolete("Use WriteVector3Int instead")]
        public void Write(Vector3Int value) => WriteVector3Int(value);

        public void WriteVector3Int(Vector3Int value)
        {
            WritePackedInt32(value.x);
            WritePackedInt32(value.y);
            WritePackedInt32(value.z);
        }

        [Obsolete("Use WriteColor instead")]
        public void Write(Color value) => WriteColor(value);

        public void WriteColor(Color value)
        {
            WriteSingle(value.r);
            WriteSingle(value.g);
            WriteSingle(value.b);
            WriteSingle(value.a);
        }

        [Obsolete("Use WriteColor32 instead")]
        public void Write(Color32 value) => WriteColor32(value);

        public void WriteColor32(Color32 value)
        {
            WriteByte(value.r);
            WriteByte(value.g);
            WriteByte(value.b);
            WriteByte(value.a);
        }

        [Obsolete("Use WriteQuaternion instead")]
        public void Write(Quaternion value) => WriteQuaternion(value);

        public void WriteQuaternion(Quaternion value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
            WriteSingle(value.z);
            WriteSingle(value.w);
        }

        [Obsolete("Use WriteRect instead")]
        public void Write(Rect value) => WriteRect(value);

        public void WriteRect(Rect value)
        {
            WriteSingle(value.xMin);
            WriteSingle(value.yMin);
            WriteSingle(value.width);
            WriteSingle(value.height);
        }

        [Obsolete("Use WritePlane instead")]
        public void Write(Plane value) => WritePlane(value);

        public void WritePlane(Plane value)
        {
            WriteVector3(value.normal);
            WriteSingle(value.distance);
        }

        [Obsolete("Use WriteRay instead")]
        public void Write(Ray value) => WriteRay(value);

        public void WriteRay(Ray value)
        {
            WriteVector3(value.origin);
            WriteVector3(value.direction);
        }

        [Obsolete("Use WriteMatrix4x4 instead")]
        public void Write(Matrix4x4 value) => WriteMatrix4x4(value);

        public void WriteMatrix4x4(Matrix4x4 value)
        {
            WriteSingle(value.m00);
            WriteSingle(value.m01);
            WriteSingle(value.m02);
            WriteSingle(value.m03);
            WriteSingle(value.m10);
            WriteSingle(value.m11);
            WriteSingle(value.m12);
            WriteSingle(value.m13);
            WriteSingle(value.m20);
            WriteSingle(value.m21);
            WriteSingle(value.m22);
            WriteSingle(value.m23);
            WriteSingle(value.m30);
            WriteSingle(value.m31);
            WriteSingle(value.m32);
            WriteSingle(value.m33);
        }

        [Obsolete("Use WriteGuid instead")]
        public void Write(Guid value) => WriteGuid(value);

        public void WriteGuid(Guid value)
        {
            byte[] data = value.ToByteArray();
            WriteBytes(data, 0, data.Length);
        }

        [Obsolete("Use WriteNetworkIdentity instead")]
        public void Write(NetworkIdentity value) => WriteNetworkIdentity(value);

        public void WriteNetworkIdentity(NetworkIdentity value)
        {
            if (value == null)
            {
                WritePackedUInt32(0);
                return;
            }
            WritePackedUInt32(value.netId);
        }

        [Obsolete("Use WriteTransform instead")]
        public void Write(Transform value) => WriteTransform(value);

        public void WriteTransform(Transform value)
        {
            if (value == null || value.gameObject == null)
            {
                WritePackedUInt32(0);
                return;
            }
            NetworkIdentity identity = value.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                WritePackedUInt32(identity.netId);
            }
            else
            {
                Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
                WritePackedUInt32(0);
            }
        }

        [Obsolete("Use WriteGameObject instead")]
        public void Write(GameObject value) => WriteGameObject(value);

        public void WriteGameObject(GameObject value)
        {
            if (value == null)
            {
                WritePackedUInt32(0);
                return;
            }
            NetworkIdentity identity = value.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                WritePackedUInt32(identity.netId);
            }
            else
            {
                Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
                WritePackedUInt32(0);
            }
        }

        public void Write<T>(T msg) where T : IMessageBase
        {
            msg.Serialize(this);
        }
    }
}
