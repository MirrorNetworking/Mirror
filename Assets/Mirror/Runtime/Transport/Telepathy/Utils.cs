namespace Telepathy
{
    public static class Utils
    {
        // fast int to byte[] conversion and vice versa
        // -> test with 100k conversions:
        //    BitConverter.GetBytes(ushort): 144ms
        //    bit shifting: 11ms
        // -> 10x speed improvement makes this optimization actually worth it
        // -> this way we don't need to allocate BinaryWriter/Reader either
        // -> 4 bytes because some people may want to send messages larger than
        //    64K bytes
        // => big endian is standard for network transmissions, and necessary
        //    for compatibility with erlang
        public static byte[] IntToBytesBigEndian(int value)
        {
            return new byte[] {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            };
        }

        // IntToBytes version that doesn't allocate a new byte[4] each time.
        // -> important for MMO scale networking performance.
        public static void IntToBytesBigEndianNonAlloc(int value, byte[] bytes)
        {
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
        }

        public static int BytesToIntBigEndian(byte[] bytes)
        {
            return
                (bytes[0] << 24) |
                (bytes[1] << 16) |
                (bytes[2] << 8) |
                bytes[3];
        }
    }
}
