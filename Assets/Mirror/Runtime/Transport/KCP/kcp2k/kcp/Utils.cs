using System.Runtime.CompilerServices;

namespace kcp2k
{
    public static partial class Utils
    {
        // encode 8 bits unsigned int
        public static int Encode8u(byte[] p, int offset, byte c)
        {
            p[0 + offset] = c;
            return 1;
        }

        // decode 8 bits unsigned int
        public static int Decode8u(byte[] p, int offset, ref byte c)
        {
            c = p[0 + offset];
            return 1;
        }

        // encode 16 bits unsigned int (lsb)
        public static int Encode16U(byte[] p, int offset, ushort w)
        {
            p[0 + offset] = (byte)(w >> 0);
            p[1 + offset] = (byte)(w >> 8);
            return 2;
        }

        // decode 16 bits unsigned int (lsb)
        public static int Decode16U(byte[] p, int offset, ref ushort c)
        {
            ushort result = 0;
            result |= p[0 + offset];
            result |= (ushort)(p[1 + offset] << 8);
            c = result;
            return 2;
        }

        // encode 32 bits unsigned int (lsb)
        public static int Encode32U(byte[] p, int offset, uint l)
        {
            p[0 + offset] = (byte)(l >> 0);
            p[1 + offset] = (byte)(l >> 8);
            p[2 + offset] = (byte)(l >> 16);
            p[3 + offset] = (byte)(l >> 24);
            return 4;
        }

        // decode 32 bits unsigned int (lsb)
        public static int Decode32U(byte[] p, int offset, ref uint c)
        {
            uint result = 0;
            result |= p[0 + offset];
            result |= (uint)(p[1 + offset] << 8);
            result |= (uint)(p[2 + offset] << 16);
            result |= (uint)(p[3 + offset] << 24);
            c = result;
            return 4;
        }

        // timediff was a macro in original Kcp. let's inline it if possible.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TimeDiff(uint later, uint earlier)
        {
            return (int)(later - earlier);
        }
    }
}
