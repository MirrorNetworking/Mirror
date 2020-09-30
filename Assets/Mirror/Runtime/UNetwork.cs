using System.Runtime.InteropServices;

namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkConnection conn, NetworkReader reader, int channelId);

    // invoke type for Cmd/Rpc
    public enum MirrorInvokeType
    {
        Command,
        ClientRpc
    }

    public static class Channels
    {
        public const int DefaultReliable = 0;
        public const int DefaultUnreliable = 1;
    }

    // -- helpers for float conversion without allocations --
    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntFloat
    {
        [FieldOffset(0)]
        public float floatValue;

        [FieldOffset(0)]
        public uint intValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntDouble
    {
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
}
