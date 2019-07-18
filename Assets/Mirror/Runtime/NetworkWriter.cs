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

        public void Write(ushort value)
        {
            Write((byte)(value & 0xFF));
            Write((byte)(value >> 8));
        }
        public void Write(uint value)
        {
            Write((byte)(value & 0xFF));
            Write((byte)((value >> 8) & 0xFF));
            Write((byte)((value >> 16) & 0xFF));
            Write((byte)((value >> 24) & 0xFF));
        }

        public void Write(ulong value) {
            Write((byte)(value & 0xFF));
            Write((byte)((value >> 8) & 0xFF));
            Write((byte)((value >> 16) & 0xFF));
            Write((byte)((value >> 24) & 0xFF));
            Write((byte)((value >> 32) & 0xFF));
            Write((byte)((value >> 40) & 0xFF));
            Write((byte)((value >> 48) & 0xFF));
            Write((byte)((value >> 56) & 0xFF));
        }

        public void Write(byte value) => stream.WriteByte(value);
        public void Write(sbyte value) => Write((byte)value);
        // write char the same way that NetworkReader reads it (2 bytes)
        public void Write(char value) => Write((ushort)value);
        public void Write(bool value) => Write((byte)(value ? 1 : 0));
        public void Write(short value) => Write((ushort)value);
        public void Write(int value) => Write((uint)value);
        public void Write(long value) => Write((ulong)value);

        public void Write(float value) {
            UIntFloat converter = new UIntFloat
            {
                floatValue = value
            };
            Write(converter.intValue);
        }

        public void Write(double value)
        {
            UIntDouble converter = new UIntDouble
            {
                doubleValue = value
            };
            Write(converter.longValue);
        }

        public void Write(decimal value)
        {
            // the only way to read it without allocations is to both read and
            // write it with the FloatConverter (which is not binary compatible
            // to writer.Write(decimal), hence why we use it here too)
            UIntDecimal converter = new UIntDecimal
            {
                decimalValue = value
            };
            Write(converter.longValue1);
            Write(converter.longValue2);
        }

        public void Write(string value)
        {
            // write 0 for null support, increment real size by 1
            // (note: original HLAPI would write "" for null strings, but if a
            //        string is null on the server then it should also be null
            //        on the client)
            if (value == null)
            {
                Write((ushort)0);
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
            Write(checked((ushort)(size + 1)));
            Write(stringBuffer, 0, size);
        }

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void Write(byte[] buffer, int offset, int count)
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
            Write(buffer, offset, count);
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
                Write((byte)value);
                return;
            }
            if (value <= 2287)
            {
                Write((byte)(((value - 240) >> 8) + 241));
                Write((byte)((value - 240) & 0xFF));
                return;
            }
            if (value <= 67823)
            {
                Write((byte)249);
                Write((byte)((value - 2288) >> 8));
                Write((byte)((value - 2288) & 0xFF));
                return;
            }
            if (value <= 16777215)
            {
                Write((byte)250);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                Write((byte)251);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                Write((byte)252);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                Write((byte)253);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                Write((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                Write((byte)254);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                Write((byte)((value >> 40) & 0xFF));
                Write((byte)((value >> 48) & 0xFF));
                return;
            }

            // all others
            {
                Write((byte)255);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                Write((byte)((value >> 40) & 0xFF));
                Write((byte)((value >> 48) & 0xFF));
                Write((byte)((value >> 56) & 0xFF));
            }
        }

        public void Write(Vector2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        public void Write(Vector3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        public void Write(Vector4 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        public void Write(Vector2Int value)
        {
            WritePackedInt32(value.x);
            WritePackedInt32(value.y);
        }

        public void Write(Vector3Int value)
        {
            WritePackedInt32(value.x);
            WritePackedInt32(value.y);
            WritePackedInt32(value.z);
        }

        public void Write(Color value)
        {
            Write(value.r);
            Write(value.g);
            Write(value.b);
            Write(value.a);
        }

        public void Write(Color32 value)
        {
            Write(value.r);
            Write(value.g);
            Write(value.b);
            Write(value.a);
        }

        public void Write(Quaternion value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        public void Write(Rect value)
        {
            Write(value.xMin);
            Write(value.yMin);
            Write(value.width);
            Write(value.height);
        }

        public void Write(Plane value)
        {
            Write(value.normal);
            Write(value.distance);
        }

        public void Write(Ray value)
        {
            Write(value.origin);
            Write(value.direction);
        }

        public void Write(Matrix4x4 value)
        {
            Write(value.m00);
            Write(value.m01);
            Write(value.m02);
            Write(value.m03);
            Write(value.m10);
            Write(value.m11);
            Write(value.m12);
            Write(value.m13);
            Write(value.m20);
            Write(value.m21);
            Write(value.m22);
            Write(value.m23);
            Write(value.m30);
            Write(value.m31);
            Write(value.m32);
            Write(value.m33);
        }

        public void Write(Guid value)
        {
            byte[] data = value.ToByteArray();
            Write(data, 0, data.Length);
        }

        public void Write(NetworkIdentity value)
        {
            if (value == null)
            {
                WritePackedUInt32(0);
                return;
            }
            WritePackedUInt32(value.netId);
        }

        public void Write(Transform value)
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

        public void Write(GameObject value)
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
