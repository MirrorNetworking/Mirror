namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkConnection conn, NetworkReader reader);

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
}
