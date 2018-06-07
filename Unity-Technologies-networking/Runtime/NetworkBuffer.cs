#if ENABLE_UNET
using System;
using System.Runtime.InteropServices;

namespace UnityEngine.Networking
{
    // A growable buffer class used by NetworkReader and NetworkWriter.
    // this is used instead of MemoryStream and BinaryReader/BinaryWriter to avoid allocations.
    class NetBuffer
    {
        byte[] m_Buffer;
        uint m_Pos;
        const int k_InitialSize = 64;
        const float k_GrowthFactor = 1.5f;
        const int k_BufferSizeWarning = 1024 * 1024 * 128;

        public uint Position { get { return m_Pos; } }
        public int Length { get { return m_Buffer.Length; } }

        public NetBuffer()
        {
            m_Buffer = new byte[k_InitialSize];
        }

        // this does NOT copy the buffer
        public NetBuffer(byte[] buffer)
        {
            m_Buffer = buffer;
        }

        public byte ReadByte()
        {
            if (m_Pos >= m_Buffer.Length)
            {
                throw new IndexOutOfRangeException("NetworkReader:ReadByte out of range:" + ToString());
            }

            return m_Buffer[m_Pos++];
        }

        public void ReadBytes(byte[] buffer, uint count)
        {
            if (m_Pos + count > m_Buffer.Length)
            {
                throw new IndexOutOfRangeException("NetworkReader:ReadBytes out of range: (" + count + ") " + ToString());
            }

            for (ushort i = 0; i < count; i++)
            {
                buffer[i] = m_Buffer[m_Pos + i];
            }
            m_Pos += count;
        }

        internal ArraySegment<byte> AsArraySegment()
        {
            return new ArraySegment<byte>(m_Buffer, 0, (int)m_Pos);
        }

        public void WriteByte(byte value)
        {
            WriteCheckForSpace(1);
            m_Buffer[m_Pos] = value;
            m_Pos += 1;
        }

        public void WriteByte2(byte value0, byte value1)
        {
            WriteCheckForSpace(2);
            m_Buffer[m_Pos] = value0;
            m_Buffer[m_Pos + 1] = value1;
            m_Pos += 2;
        }

        public void WriteByte4(byte value0, byte value1, byte value2, byte value3)
        {
            WriteCheckForSpace(4);
            m_Buffer[m_Pos] = value0;
            m_Buffer[m_Pos + 1] = value1;
            m_Buffer[m_Pos + 2] = value2;
            m_Buffer[m_Pos + 3] = value3;
            m_Pos += 4;
        }

        public void WriteByte8(byte value0, byte value1, byte value2, byte value3, byte value4, byte value5, byte value6, byte value7)
        {
            WriteCheckForSpace(8);
            m_Buffer[m_Pos] = value0;
            m_Buffer[m_Pos + 1] = value1;
            m_Buffer[m_Pos + 2] = value2;
            m_Buffer[m_Pos + 3] = value3;
            m_Buffer[m_Pos + 4] = value4;
            m_Buffer[m_Pos + 5] = value5;
            m_Buffer[m_Pos + 6] = value6;
            m_Buffer[m_Pos + 7] = value7;
            m_Pos += 8;
        }

        // every other Write() function in this class writes implicitly at the end-marker m_Pos.
        // this is the only Write() function that writes to a specific location within the buffer
        public void WriteBytesAtOffset(byte[] buffer, ushort targetOffset, ushort count)
        {
            uint newEnd = (uint)(count + targetOffset);

            WriteCheckForSpace((ushort)newEnd);

            if (targetOffset == 0 && count == buffer.Length)
            {
                buffer.CopyTo(m_Buffer, (int)m_Pos);
            }
            else
            {
                //CopyTo doesnt take a count :(
                for (int i = 0; i < count; i++)
                {
                    m_Buffer[targetOffset + i] = buffer[i];
                }
            }

            // although this writes within the buffer, it could move the end-marker
            if (newEnd > m_Pos)
            {
                m_Pos = newEnd;
            }
        }

        public void WriteBytes(byte[] buffer, ushort count)
        {
            WriteCheckForSpace(count);

            if (count == buffer.Length)
            {
                buffer.CopyTo(m_Buffer, (int)m_Pos);
            }
            else
            {
                //CopyTo doesnt take a count :(
                for (int i = 0; i < count; i++)
                {
                    m_Buffer[m_Pos + i] = buffer[i];
                }
            }
            m_Pos += count;
        }

        void WriteCheckForSpace(ushort count)
        {
            if (m_Pos + count < m_Buffer.Length)
                return;

            int newLen = (int)Math.Ceiling(m_Buffer.Length * k_GrowthFactor);
            while (m_Pos + count >= newLen)
            {
                newLen = (int)Math.Ceiling(newLen * k_GrowthFactor);
                if (newLen > k_BufferSizeWarning)
                {
                    Debug.LogWarning("NetworkBuffer size is " + newLen + " bytes!");
                }
            }

            // only do the copy once, even if newLen is increased multiple times
            byte[] tmp = new byte[newLen];
            m_Buffer.CopyTo(tmp, 0);
            m_Buffer = tmp;
        }

        public void FinishMessage()
        {
            // two shorts (size and msgType) are in header.
            ushort sz = (ushort)(m_Pos - (sizeof(ushort) * 2));
            m_Buffer[0] = (byte)(sz & 0xff);
            m_Buffer[1] = (byte)((sz >> 8) & 0xff);
        }

        public void SeekZero()
        {
            m_Pos = 0;
        }

        public void Replace(byte[] buffer)
        {
            m_Buffer = buffer;
            m_Pos = 0;
        }

        public override string ToString()
        {
            return String.Format("NetBuf sz:{0} pos:{1}", m_Buffer.Length, m_Pos);
        }
    } // end NetBuffer

    // -- helpers for float conversion --
    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntFloat
    {
        [FieldOffset(0)]
        public float floatValue;

        [FieldOffset(0)]
        public uint intValue;

        [FieldOffset(0)]
        public double doubleValue;

        [FieldOffset(0)]
        public ulong longValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntDecimal
    {
        [FieldOffset(0)]
        public ulong longValue1;

        [FieldOffset(8)]
        public ulong longValue2;

        [FieldOffset(0)]
        public decimal decimalValue;
    }

    internal class FloatConversion
    {
        public static float ToSingle(uint value)
        {
            UIntFloat uf = new UIntFloat();
            uf.intValue = value;
            return uf.floatValue;
        }

        public static double ToDouble(ulong value)
        {
            UIntFloat uf = new UIntFloat();
            uf.longValue = value;
            return uf.doubleValue;
        }

        public static decimal ToDecimal(ulong value1, ulong value2)
        {
            UIntDecimal uf = new UIntDecimal();
            uf.longValue1 = value1;
            uf.longValue2 = value2;
            return uf.decimalValue;
        }
    }
}

#endif
