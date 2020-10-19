namespace Mirror.KCP
{

    // encode data into a byte[]
    struct Decoder
    {
        public int Position { get; set; }

        private readonly byte[] buffer;

        public Decoder(byte[] buffer, int position)
        {
            Position = position;
            this.buffer = buffer;
        }

        // decode 8 bits unsigned int
        public byte Decode8U()
        {
            return buffer[Position++];
        }

        /* decode 16 bits unsigned int (lsb) */
        public ushort Decode16U()
        {
            ushort result = 0;
            result |= buffer[Position++];
            result |= (ushort)(buffer[Position++] << 8);
            return result;
        }

        /* decode 32 bits unsigned int (lsb) */
        public uint Decode32U()
        {
            uint result = 0;
            result |= buffer[Position++];
            result |= (uint)(buffer[Position++] << 8);
            result |= (uint)(buffer[Position++] << 16);
            result |= (uint)(buffer[Position++] << 24);
            return result;
        }

        /* decode 32 bits unsigned int (lsb) */
        public ulong Decode64U()
        {
            ulong result = 0;
            result |= buffer[Position++];
            result |= (ulong)buffer[Position++] << 8;
            result |= (ulong)buffer[Position++] << 16;
            result |= (ulong)buffer[Position++] << 24;
            result |= (ulong)buffer[Position++] << 32;
            result |= (ulong)buffer[Position++] << 40;
            result |= (ulong)buffer[Position++] << 48;
            result |= (ulong)buffer[Position++] << 56;
            return result;
        }
    }
}