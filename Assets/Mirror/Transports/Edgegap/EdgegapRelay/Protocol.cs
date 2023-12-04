// relay protocol definitions
namespace Edgegap
{
    public enum ConnectionState : byte
    {
        Disconnected = 0,   // until the user calls connect()
        Checking = 1,       // recently connected, validation in progress
        Valid = 2,          // validation succeeded
        Invalid = 3,        // validation rejected by tower
        SessionTimeout = 4, // session owner timed out
        Error = 5,          // other error
    }

    public enum MessageType : byte
    {
        Ping = 1,
        Data = 2
    }

    public static class Protocol
    {
        // MTU: relay adds up to 13 bytes of metadata in the worst case.
        public const int Overhead = 13;

        // ping interval should be between 100 ms and 1 second.
        // faster ping gives faster authentication, but higher bandwidth.
        public const float PingInterval = 0.5f;
    }
}
