namespace Octodiff.Core
{
    public static class Adler32RollingChecksumV2
    {
        const ushort Modulus = 65521;

        public static uint Calculate(byte[] block, int offset, int count)
        {
            int a = 1;
            int b = 0;
            for (int i = offset; i < offset + count; i++)
            {
                byte z = block[i];
                a = (z + a) % Modulus;
                b = (b + a) % Modulus;
            }
            return (uint)((b << 16) | a);
        }

        public static uint Rotate(uint checksum, byte remove, byte add, int chunkSize)
        {
            ushort b = (ushort)(checksum >> 16 & 0xffff);
            ushort a = (ushort)(checksum & 0xffff);

            a = (ushort)((a - remove + add) % Modulus);
            b = (ushort)((b - (chunkSize * remove) + a - 1) % Modulus);

            return (uint)((b << 16) | a);
        }
    }
}
