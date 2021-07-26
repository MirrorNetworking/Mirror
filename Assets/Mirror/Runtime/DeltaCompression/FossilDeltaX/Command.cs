namespace FossilDeltaX
{
    public static class Command
    {
        public const byte CHECKSUM = 0x02; // ';' in original FossilDelta
        public const byte COPY = 0x03;     // '@' in original FossilDelta
        public const byte COPY_END = 0x04; // ',' in original FossilDelta
        public const byte INSERT = 0x05;   // ':' in original FossilDelta
    }
}