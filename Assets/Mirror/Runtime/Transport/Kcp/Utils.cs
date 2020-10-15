using System;

namespace Mirror.KCP
{
    public static class Utils
    {
        // encode 8 bits unsigned int
        public static int Encode8u(byte[] p, int offset, byte c)
        {
            p[0 + offset] = c;
            return 1;
        }

        // decode 8 bits unsigned int
        public static (int offset, byte value) Decode8u(byte[] p, int offset)
        {
            return (offset + 1, p[0 + offset]);
        }

        /* encode 16 bits unsigned int (lsb) */
        public static int Encode16U(byte[] p, int offset, ushort w)
        {
            p[0 + offset] = (byte)(w >> 0);
            p[1 + offset] = (byte)(w >> 8);
            return 2;
        }

        /* decode 16 bits unsigned int (lsb) */
        public static (int offset, ushort value) Decode16U(byte[] p, int offset)
        {
            ushort result = 0;
            result |= p[0 + offset];
            result |= (ushort)(p[1 + offset] << 8);
            return (offset + 2, result);
        }

        /* encode 32 bits unsigned int (lsb) */
        public static int Encode32U(byte[] p, int offset, uint l)
        {
            p[0 + offset] = (byte)(l >> 0);
            p[1 + offset] = (byte)(l >> 8);
            p[2 + offset] = (byte)(l >> 16);
            p[3 + offset] = (byte)(l >> 24);
            return 4;
        }

        /* decode 32 bits unsigned int (lsb) */
        public static (int offset, uint value) Decode32U(byte[] p, int offset)
        {
            uint result = 0;
            result |= p[0 + offset];
            result |= (uint)(p[1 + offset] << 8);
            result |= (uint)(p[2 + offset] << 16);
            result |= (uint)(p[3 + offset] << 24);
            return (offset + 4, result);
        }

        /* encode 32 bits unsigned int (lsb) */
        public static int Encode64U(byte[] p, int offset, ulong l)
        {
            p[0 + offset] = (byte)(l >> 0);
            p[1 + offset] = (byte)(l >> 8);
            p[2 + offset] = (byte)(l >> 16);
            p[3 + offset] = (byte)(l >> 24);
            p[4 + offset] = (byte)(l >> 32);
            p[5 + offset] = (byte)(l >> 40);
            p[6 + offset] = (byte)(l >> 48);
            p[7 + offset] = (byte)(l >> 56);
            return 8;
        }

        /* decode 32 bits unsigned int (lsb) */
        public static (int offset, ulong value) Decode64U(byte[] p, int offset)
        {
            ulong result = 0;
            result |= p[0 + offset];
            result |= (ulong)p[1 + offset] << 8;
            result |= (ulong)p[2 + offset] << 16;
            result |= (ulong)p[3 + offset] << 24;
            result |= (ulong)p[4 + offset] << 32;
            result |= (ulong)p[5 + offset] << 40;
            result |= (ulong)p[6 + offset] << 48;
            result |= (ulong)p[7 + offset] << 56;
            return (offset + 8, result);
        }

        public static int Clamp(int value, int lower, int upper)
        {
            return Math.Min(Math.Max(lower, value), upper);
        }

        public static int TimeDiff(uint later, uint earlier)
        {
            return ((int)(later - earlier));
        }

        public static bool Equal(ArraySegment<byte> seg1, ArraySegment<byte> seg2)
        {
            if (seg1.Count != seg2.Count)
                return false;

            for (int i=0; i< seg1.Count; i++)
            {
                if (seg1.Array[i + seg1.Offset] != seg2.Array[i+seg2.Offset])                
                    return false;
            }
            return true;
        }
    }
}
