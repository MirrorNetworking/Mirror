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

        // create writer immediately with it's own buffer so no one can mess with it and so that we can resize it.
        readonly BinaryWriter writer = new BinaryWriter(new MemoryStream(), encoding);

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position { get { return (int)writer.BaseStream.Position; } set { writer.BaseStream.Position = value; } }

        // MemoryStream has 3 values: Position, Length and Capacity.
        // Position is used to indicate where we are writing
        // Length is how much data we have written
        // capacity is how much memory we have allocated
        // ToArray returns all the data we have written,  regardless of the current position
        public byte[] ToArray()
        {
            writer.Flush();
            return ((MemoryStream)writer.BaseStream).ToArray();
        }

        // reset both the position and length of the stream,  but leaves the capacity the same
        // so that we can reuse this writer without extra allocations
        public void SetLength(long value)
        {
            ((MemoryStream)writer.BaseStream).SetLength(value);
        }

        public void Write(byte value) => writer.Write(value);
        public void Write(sbyte value) => writer.Write(value);
        public void Write(char value) => writer.Write(value);
        public void Write(bool value) => writer.Write(value);
        public void Write(short value) => writer.Write(value);
        public void Write(ushort value) => writer.Write(value);
        public void Write(int value) => writer.Write(value);
        public void Write(uint value) => writer.Write(value);
        public void Write(long value) => writer.Write(value);
        public void Write(ulong value) => writer.Write(value);
        public void Write(float value) => writer.Write(value);
        public void Write(double value) => writer.Write(value);
        public void Write(decimal value) => writer.Write(value);

        public void Write(string value)
        {
            // BinaryWriter doesn't support null strings, so let's write an extra boolean for that
            // (note: original HLAPI would write "" for null strings, but if a string is null on the server then it
            //        should also be null on the client)
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void Write(byte[] buffer, int offset, int count)
        {
            // no null check because we would need to write size info for that too (hence WriteBytesAndSize)
            writer.Write(buffer, offset, count);
        }

        // for byte arrays with dynamic size, where the reader doesn't know how many will come
        // (like an inventory with different items etc.)
        public void WriteBytesAndSize(byte[] buffer, int offset, int count)
        {
            // null is supported because [SyncVar]s might be structs with null byte[] arrays
            // (writing a size=0 empty array is not the same, the server and client would be out of sync)
            // (using size=-1 for null would limit max size to 32kb instead of 64kb)
            if (buffer == null)
            {
                writer.Write(false); // notNull?
                return;
            }
            if (count < 0)
            {
                Debug.LogError("NetworkWriter WriteBytesAndSize: size " + count + " cannot be negative");
                return;
            }

            writer.Write(true); // notNull?
            WritePackedUInt32((uint)count);
            writer.Write(buffer, offset, count);
        }

        // Weaver needs a write function with just one byte[] parameter
        // (we don't name it .Write(byte[]) because it's really a WriteBytesAndSize since we write size / null info too)
        public void WriteBytesAndSize(byte[] buffer)
        {
            // buffer might be null, so we can't use .Length in that case
            WriteBytesAndSize(buffer, 0, buffer != null ? buffer.Length : 0);
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
                Write((byte)((value - 240) / 256 + 241));
                Write((byte)((value - 240) % 256));
                return;
            }
            if (value <= 67823)
            {
                Write((byte)249);
                Write((byte)((value - 2288) / 256));
                Write((byte)((value - 2288) % 256));
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
            Write(value.direction);
            Write(value.origin);
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
            writer.Write(value.ToByteArray());
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
