using System.Net.Sockets;

namespace kcp2k
{
    public static class Extensions
    {
        // 100k attempts of 1 KB increases = default + 100 MB max
        public static void SetReceiveBufferToOSLimit(this Socket socket, int stepSize = 1024, int attempts = 100_000)
        {
            // setting a too large size throws a socket exception.
            // so let's keep increasing until we encounter it.
            for (int i = 0; i < attempts; ++i)
            {
                // increase in 1 KB steps
                try { socket.ReceiveBufferSize += stepSize; }
                catch (SocketException) { break; }
            }
        }

        // 100k attempts of 1 KB increases = default + 100 MB max
        public static void SetSendBufferToOSLimit(this Socket socket, int stepSize = 1024, int attempts = 100_000)
        {
            // setting a too large size throws a socket exception.
            // so let's keep increasing until we encounter it.
            for (int i = 0; i < attempts; ++i)
            {
                // increase in 1 KB steps
                try { socket.SendBufferSize += stepSize; }
                catch (SocketException) { break; }
            }
        }
    }
}