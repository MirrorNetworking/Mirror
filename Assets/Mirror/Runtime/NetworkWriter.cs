using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // Binary stream Writer. Supports simple types, buffers, arrays, structs, and nested types
    public class NetworkWriter
    {
        public const int MaxStringLength = 1024 * 32;

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

        public void WriteByte(byte value) => stream.WriteByte(value);

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            // no null check because we would need to write size info for that too (hence WriteBytesAndSize)
            stream.Write(buffer, offset, count);
        }

        public void WriteUInt32(uint value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 8) & 0xFF));
            WriteByte((byte)((value >> 16) & 0xFF));
            WriteByte((byte)((value >> 24) & 0xFF));
        }

        public void WriteInt32(int value) => WriteUInt32((uint)value);

        public void WriteUInt64(ulong value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 8) & 0xFF));
            WriteByte((byte)((value >> 16) & 0xFF));
            WriteByte((byte)((value >> 24) & 0xFF));
            WriteByte((byte)((value >> 32) & 0xFF));
            WriteByte((byte)((value >> 40) & 0xFF));
            WriteByte((byte)((value >> 48) & 0xFF));
            WriteByte((byte)((value >> 56) & 0xFF));
        }

        public void WriteInt64(long value) => WriteUInt64((ulong)value);

        #region Obsoletes

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteUInt16 instead")]
        public void Write(ushort value) => this.WriteUInt16(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteUInt32 instead")]
        public void Write(uint value) => WriteUInt32(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteUInt64 instead")]
        public void Write(ulong value) => WriteUInt64(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteByte instead")]
        public void Write(byte value) => stream.WriteByte(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteSByte instead")]
        public void Write(sbyte value) => WriteByte((byte)value);

        // write char the same way that NetworkReader reads it (2 bytes)
        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteChar instead")]
        public void Write(char value) => this.WriteUInt16((ushort)value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteBoolean instead")]
        public void Write(bool value) => WriteByte((byte)(value ? 1 : 0));

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteInt16 instead")]
        public void Write(short value) => this.WriteUInt16((ushort)value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteInt32 instead")]
        public void Write(int value) => WriteUInt32((uint)value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteInt64 instead")]
        public void Write(long value) => WriteUInt64((ulong)value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteSingle instead")]
        public void Write(float value) => this.WriteSingle(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteDouble instead")]
        public void Write(double value) => this.WriteDouble(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteDecimal instead")]
        public void Write(decimal value) => this.WriteDecimal(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteString instead")]
        public void Write(string value) => this.WriteString(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteBytes instead")]
        public void Write(byte[] buffer, int offset, int count) => WriteBytes(buffer, offset, count);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteVector2 instead")]
        public void Write(Vector2 value) => this.WriteVector2(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteVector3 instead")]
        public void Write(Vector3 value) => this.WriteVector3(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteVector4 instead")]
        public void Write(Vector4 value) => this.WriteVector4(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteVector2Int instead")]
        public void Write(Vector2Int value) => this.WriteVector2Int(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteVector3Int instead")]
        public void Write(Vector3Int value) => this.WriteVector3Int(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteColor instead")]
        public void Write(Color value) => this.WriteColor(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteColor32 instead")]
        public void Write(Color32 value) => this.WriteColor32(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteQuaternion instead")]
        public void Write(Quaternion value) => this.WriteQuaternion(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteRect instead")]
        public void Write(Rect value) => this.WriteRect(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WritePlane instead")]
        public void Write(Plane value) => this.WritePlane(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteRay instead")]
        public void Write(Ray value) => this.WriteRay(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteMatrix4x4 instead")]
        public void Write(Matrix4x4 value) => this.WriteMatrix4x4(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteGuid instead")]
        public void Write(Guid value) => this.WriteGuid(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteNetworkIdentity instead")]
        public void Write(NetworkIdentity value) => this.WriteNetworkIdentity(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteTransform instead")]
        public void Write(Transform value) => this.WriteTransform(value);

        // Deprecated 03/03/2019
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteGameObject instead")]
        public void Write(GameObject value) => this.WriteGameObject(value);

        #endregion
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

        public static void WriteChar(this NetworkWriter writer, char value) => writer.WriteUInt16((ushort)value);

        public static void WriteBoolean(this NetworkWriter writer, bool value) => writer.WriteByte((byte)(value ? 1 : 0));

        public static void WriteUInt16(this NetworkWriter writer, ushort value)
        {
            writer.WriteByte((byte)(value & 0xFF));
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
                writer.WriteUInt16((ushort)0);
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
                writer.WriteByte((byte)((value - 240) & 0xFF));
                return;
            }
            if (value <= 67823)
            {
                writer.WriteByte((byte)249);
                writer.WriteByte((byte)((value - 2288) >> 8));
                writer.WriteByte((byte)((value - 2288) & 0xFF));
                return;
            }
            if (value <= 16777215)
            {
                writer.WriteByte((byte)250);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                writer.WriteByte((byte)251);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                writer.WriteByte((byte)252);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                writer.WriteByte((byte)253);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                writer.WriteByte((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                writer.WriteByte((byte)254);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                writer.WriteByte((byte)((value >> 40) & 0xFF));
                writer.WriteByte((byte)((value >> 48) & 0xFF));
                return;
            }

            // all others
            {
                writer.WriteByte((byte)255);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                writer.WriteByte((byte)((value >> 40) & 0xFF));
                writer.WriteByte((byte)((value >> 48) & 0xFF));
                writer.WriteByte((byte)((value >> 56) & 0xFF));
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
            if (value == null || value.gameObject == null)
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
                Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
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
                Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity");
                writer.WritePackedUInt32(0);
            }
        }

        public static void WriteUri(this NetworkWriter writer, Uri uri)
        {
            writer.WriteString(uri.ToString());
        }

        public static void WriteMessage<T>(this NetworkWriter writer, T msg) where T : IMessageBase
        {
            msg.Serialize(writer);
        }

        // Deprecated 02/06/2020
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use WriteMessage<T> instead")]
        public static void Write<T>(this NetworkWriter writer, T msg) where T : IMessageBase
        {
            WriteMessage(writer, msg);
        }
    }
}
