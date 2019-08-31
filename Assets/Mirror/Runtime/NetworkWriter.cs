using System;
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

        public void WriteUInt16(ushort value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)(value >> 8));
        }

        public void WriteUInt32(uint value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 8) & 0xFF));
            WriteByte((byte)((value >> 16) & 0xFF));
            WriteByte((byte)((value >> 24) & 0xFF));
        }

        public void WriteInt32(int value) => WriteUInt32((uint)value);

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

        public void WriteInt64(long value) => WriteUInt64((ulong)value);
        
        #region Obsoletes
        [Obsolete("Use WriteUInt16 instead")]
        public void Write(ushort value) => WriteUInt16(value);

        [Obsolete("Use WriteUInt32 instead")]
        public void Write(uint value) => WriteUInt32(value);

        [Obsolete("Use WriteUInt64 instead")]
        public void Write(ulong value) => WriteUInt64(value);

        [Obsolete("Use WriteByte instead")]
        public void Write(byte value) => stream.WriteByte(value);

        [Obsolete("Use WriteSByte instead")]
        public void Write(sbyte value) => WriteByte((byte)value);

        // write char the same way that NetworkReader reads it (2 bytes)
        [Obsolete("Use WriteChar instead")]
        public void Write(char value) => WriteUInt16((ushort)value);

        [Obsolete("Use WriteBoolean instead")]
        public void Write(bool value) => WriteByte((byte)(value ? 1 : 0));

        [Obsolete("Use WriteInt16 instead")]
        public void Write(short value) => WriteUInt16((ushort)value);

        [Obsolete("Use WriteInt32 instead")]
        public void Write(int value) => WriteUInt32((uint)value);

        [Obsolete("Use WriteInt64 instead")]
        public void Write(long value) => WriteUInt64((ulong)value);

        [Obsolete("Use WriteSingle instead")]
        public void Write(float value) => this.WriteSingle(value);
        
        [Obsolete("Use WriteDouble instead")]
        public void Write(double value) => this.WriteDouble(value);

        [Obsolete("Use WriteDecimal instead")]
        public void Write(decimal value) => this.WriteDecimal(value);

        [Obsolete("Use WriteString instead")]
        public void Write(string value) => this.WriteString(value);

        [Obsolete("Use WriteBytes instead")]
        public void Write(byte[] buffer, int offset, int count) => WriteBytes(buffer, offset, count);

        [Obsolete("Use WriteVector2 instead")]
        public void Write(Vector2 value) => this.WriteVector2(value);

        [Obsolete("Use WriteVector3 instead")]
        public void Write(Vector3 value) => this.WriteVector3(value);

        [Obsolete("Use WriteVector4 instead")]
        public void Write(Vector4 value) => this.WriteVector4(value);

        [Obsolete("Use WriteVector2Int instead")]
        public void Write(Vector2Int value) => this.WriteVector2Int(value);

        [Obsolete("Use WriteVector3Int instead")]
        public void Write(Vector3Int value) => this.WriteVector3Int(value);

        [Obsolete("Use WriteColor instead")]
        public void Write(Color value) => this.WriteColor(value);

        [Obsolete("Use WriteColor32 instead")]
        public void Write(Color32 value) => this.WriteColor32(value);

        [Obsolete("Use WriteQuaternion instead")]
        public void Write(Quaternion value) => this.WriteQuaternion(value);

        [Obsolete("Use WriteRect instead")]
        public void Write(Rect value) => this.WriteRect(value);

        [Obsolete("Use WritePlane instead")]
        public void Write(Plane value) => this.WritePlane(value);

        [Obsolete("Use WriteRay instead")]
        public void Write(Ray value) => this.WriteRay(value);

        [Obsolete("Use WriteMatrix4x4 instead")]
        public void Write(Matrix4x4 value) => this.WriteMatrix4x4(value);

        [Obsolete("Use WriteGuid instead")]
        public void Write(Guid value) => this.WriteGuid(value);

        [Obsolete("Use WriteNetworkIdentity instead")]
        public void Write(NetworkIdentity value) => this.WriteNetworkIdentity(value);

        [Obsolete("Use WriteTransform instead")]
        public void Write(Transform value) => this.WriteTransform(value);

        [Obsolete("Use WriteGameObject instead")]
        public void Write(GameObject value) => this.WriteGameObject(value);

        #endregion
    }
}
