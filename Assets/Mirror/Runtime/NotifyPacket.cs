namespace Mirror
{
    // header for notify packet
    public struct NotifyPacket
    {
        public ushort Sequence;
        public ushort ReceiveSequence;
        public ulong AckMask;
    }


    public static class NotifyPacketSerializer
    {
        public static void WriteNotifyPacket(this NetworkWriter writer, NotifyPacket packet)
        {
            writer.WriteUInt16(packet.Sequence);
            writer.WriteUInt16(packet.ReceiveSequence);
            writer.WriteUInt64(packet.AckMask);
        }

        public static NotifyPacket ReadNotifyPacket(this NetworkReader reader)
        {
            return new NotifyPacket
            {
                Sequence = reader.ReadUInt16(),
                ReceiveSequence = reader.ReadUInt16(),
                AckMask = reader.ReadUInt64()
            };
        }
    }
}