#if ENABLE_UNET
using System;
using System.Text;
using UnityEngine;

namespace UnityEngine.Networking
{
    /*
    // Binary stream Writer. Supports simple types, buffers, arrays, structs, and nested types
        */
    public class NetworkWriter
    {
        const int k_MaxStringLength = 1024 * 32;
        NetBuffer m_Buffer;
        static Encoding s_Encoding;
        static byte[] s_StringWriteBuffer;

        public NetworkWriter()
        {
            m_Buffer = new NetBuffer();
            if (s_Encoding == null)
            {
                s_Encoding = new UTF8Encoding();
                s_StringWriteBuffer = new byte[k_MaxStringLength];
            }
        }

        public NetworkWriter(byte[] buffer)
        {
            m_Buffer = new NetBuffer(buffer);
            if (s_Encoding == null)
            {
                s_Encoding = new UTF8Encoding();
                s_StringWriteBuffer = new byte[k_MaxStringLength];
            }
        }

        public short Position { get { return (short)m_Buffer.Position; } }

        public byte[] ToArray()
        {
            var newArray = new byte[m_Buffer.AsArraySegment().Count];
            Array.Copy(m_Buffer.AsArraySegment().Array, newArray, m_Buffer.AsArraySegment().Count);
            return newArray;
        }

        public byte[] AsArray()
        {
            return AsArraySegment().Array;
        }

        internal ArraySegment<byte> AsArraySegment()
        {
            return m_Buffer.AsArraySegment();
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki

        public void WritePackedUInt32(UInt32 value)
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

            // all other values of uint
            Write((byte)251);
            Write((byte)(value & 0xFF));
            Write((byte)((value >> 8) & 0xFF));
            Write((byte)((value >> 16) & 0xFF));
            Write((byte)((value >> 24) & 0xFF));
        }

        public void WritePackedUInt64(UInt64 value)
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

        public void Write(NetworkInstanceId value)
        {
            WritePackedUInt32(value.Value);
        }

        public void Write(NetworkSceneId value)
        {
            WritePackedUInt32(value.Value);
        }

        public void Write(char value)
        {
            m_Buffer.WriteByte((byte)value);
        }

        public void Write(byte value)
        {
            m_Buffer.WriteByte(value);
        }

        public void Write(sbyte value)
        {
            m_Buffer.WriteByte((byte)value);
        }

        public void Write(short value)
        {
            m_Buffer.WriteByte2((byte)(value & 0xff), (byte)((value >> 8) & 0xff));
        }

        public void Write(ushort value)
        {
            m_Buffer.WriteByte2((byte)(value & 0xff), (byte)((value >> 8) & 0xff));
        }

        public void Write(int value)
        {
            // little endian...
            m_Buffer.WriteByte4(
                (byte)(value & 0xff),
                (byte)((value >> 8) & 0xff),
                (byte)((value >> 16) & 0xff),
                (byte)((value >> 24) & 0xff));
        }

        public void Write(uint value)
        {
            m_Buffer.WriteByte4(
                (byte)(value & 0xff),
                (byte)((value >> 8) & 0xff),
                (byte)((value >> 16) & 0xff),
                (byte)((value >> 24) & 0xff));
        }

        public void Write(long value)
        {
            m_Buffer.WriteByte8(
                (byte)(value & 0xff),
                (byte)((value >> 8) & 0xff),
                (byte)((value >> 16) & 0xff),
                (byte)((value >> 24) & 0xff),
                (byte)((value >> 32) & 0xff),
                (byte)((value >> 40) & 0xff),
                (byte)((value >> 48) & 0xff),
                (byte)((value >> 56) & 0xff));
        }

        public void Write(ulong value)
        {
            m_Buffer.WriteByte8(
                (byte)(value & 0xff),
                (byte)((value >> 8) & 0xff),
                (byte)((value >> 16) & 0xff),
                (byte)((value >> 24) & 0xff),
                (byte)((value >> 32) & 0xff),
                (byte)((value >> 40) & 0xff),
                (byte)((value >> 48) & 0xff),
                (byte)((value >> 56) & 0xff));
        }

        static UIntFloat s_FloatConverter;

        public void Write(float value)
        {
            s_FloatConverter.floatValue = value;
            Write(s_FloatConverter.intValue);
        }

        public void Write(double value)
        {
            s_FloatConverter.doubleValue = value;
            Write(s_FloatConverter.longValue);
        }

        public void Write(decimal value)
        {
            Int32[] bits = decimal.GetBits(value);
            Write(bits[0]);
            Write(bits[1]);
            Write(bits[2]);
            Write(bits[3]);
        }

        public void Write(string value)
        {
            if (value == null)
            {
                m_Buffer.WriteByte2(0, 0);
                return;
            }

            int len = s_Encoding.GetByteCount(value);

            if (len >= k_MaxStringLength)
            {
                throw new IndexOutOfRangeException("Serialize(string) too long: " + value.Length);
            }

            Write((ushort)(len));
            int numBytes = s_Encoding.GetBytes(value, 0, value.Length, s_StringWriteBuffer, 0);
            m_Buffer.WriteBytes(s_StringWriteBuffer, (ushort)numBytes);
        }

        public void Write(bool value)
        {
            if (value)
                m_Buffer.WriteByte(1);
            else
                m_Buffer.WriteByte(0);
        }

        public void Write(byte[] buffer, int count)
        {
            if (count > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter Write: buffer is too large (" + count + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }
            m_Buffer.WriteBytes(buffer, (UInt16)count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (count > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter Write: buffer is too large (" + count + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }
            m_Buffer.WriteBytesAtOffset(buffer, (ushort)offset, (ushort)count);
        }

        public void WriteBytesAndSize(byte[] buffer, int count)
        {
            if (buffer == null || count == 0)
            {
                Write((UInt16)0);
                return;
            }

            if (count > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter WriteBytesAndSize: buffer is too large (" + count + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }

            Write((UInt16)count);
            m_Buffer.WriteBytes(buffer, (UInt16)count);
        }

        //NOTE: this will write the entire buffer.. including trailing empty space!
        public void WriteBytesFull(byte[] buffer)
        {
            if (buffer == null)
            {
                Write((UInt16)0);
                return;
            }
            if (buffer.Length > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter WriteBytes: buffer is too large (" + buffer.Length + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }
            Write((UInt16)buffer.Length);
            m_Buffer.WriteBytes(buffer, (UInt16)buffer.Length);
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

        public void Write(NetworkHash128 value)
        {
            Write(value.i0);
            Write(value.i1);
            Write(value.i2);
            Write(value.i3);
            Write(value.i4);
            Write(value.i5);
            Write(value.i6);
            Write(value.i7);
            Write(value.i8);
            Write(value.i9);
            Write(value.i10);
            Write(value.i11);
            Write(value.i12);
            Write(value.i13);
            Write(value.i14);
            Write(value.i15);
        }

        public void Write(NetworkIdentity value)
        {
            if (value == null)
            {
                WritePackedUInt32(0);
                return;
            }
            Write(value.netId);
        }

        public void Write(Transform value)
        {
            if (value == null || value.gameObject == null)
            {
                WritePackedUInt32(0);
                return;
            }
            var uv = value.gameObject.GetComponent<NetworkIdentity>();
            if (uv != null)
            {
                Write(uv.netId);
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity"); }
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
            var uv = value.GetComponent<NetworkIdentity>();
            if (uv != null)
            {
                Write(uv.netId);
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity"); }
                WritePackedUInt32(0);
            }
        }

        public void Write(MessageBase msg)
        {
            msg.Serialize(this);
        }

        public void SeekZero()
        {
            m_Buffer.SeekZero();
        }

        public void StartMessage(short msgType)
        {
            SeekZero();

            // two bytes for size, will be filled out in FinishMessage
            m_Buffer.WriteByte2(0, 0);

            // two bytes for message type
            Write(msgType);
        }

        public void FinishMessage()
        {
            // writes correct size into space at start of buffer
            m_Buffer.FinishMessage();
        }
    };
}
#endif //ENABLE_UNET
