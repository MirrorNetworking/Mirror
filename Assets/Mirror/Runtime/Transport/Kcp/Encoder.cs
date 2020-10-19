namespace Mirror.KCP
{

    // encode data into a byte[]
    struct Encoder
    {
        public int Position { get; set; }

        private readonly byte[] buffer;

        public Encoder(byte[] buffer, int position)
        {
            Position = position;
            this.buffer = buffer;
        }

        // encode 8 bits unsigned int
        public void Encode8U(byte c)
        {
            buffer[Position++] = c;
        }

        /* encode 16 bits unsigned int (lsb) */
        public void Encode16U(ushort w)
        {
            buffer[Position++] = (byte)(w >> 0);
            buffer[Position++] = (byte)(w >> 8);
        }

        /* encode 32 bits unsigned int (lsb) */
        public void Encode32U(uint l)
        {
            buffer[Position++] = (byte)(l >> 0);
            buffer[Position++] = (byte)(l >> 8);
            buffer[Position++] = (byte)(l >> 16);
            buffer[Position++] = (byte)(l >> 24);
        }

        /* encode 32 bits unsigned int (lsb) */
        public void Encode64U(ulong l)
        {
            buffer[Position++] = (byte)(l >> 0);
            buffer[Position++] = (byte)(l >> 8);
            buffer[Position++] = (byte)(l >> 16);
            buffer[Position++] = (byte)(l >> 24);
            buffer[Position++] = (byte)(l >> 32);
            buffer[Position++] = (byte)(l >> 40);
            buffer[Position++] = (byte)(l >> 48);
            buffer[Position++] = (byte)(l >> 56);
        }
    }
}