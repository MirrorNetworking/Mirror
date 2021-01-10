namespace kcp2k
{
    // channel type and header for raw messages
    public enum KcpChannel : byte
    {
        // don't react on 0x00. might help to filter out random noise.
        Reliable = 0x01,
        Unreliable = 0x02
    }
}