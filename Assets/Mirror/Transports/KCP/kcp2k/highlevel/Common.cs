using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public static class Common
    {
        // helper function to resolve host to IPAddress
        public static bool ResolveHostname(string hostname, out IPAddress[] addresses)
        {
            try
            {
                // NOTE: dns lookup is blocking. this can take a second.
                addresses = Dns.GetHostAddresses(hostname);
                return addresses.Length >= 1;
            }
            catch (SocketException exception)
            {
                Log.Info($"Failed to resolve host: {hostname} reason: {exception}");
                addresses = null;
                return false;
            }
        }

        // if connections drop under heavy load, increase to OS limit.
        // if still not enough, increase the OS limit.
        public static void MaximizeSocketBuffers(Socket socket)
        {
            // log initial size for comparison.
            // remember initial size for log comparison
            int initialReceive = socket.ReceiveBufferSize;
            int initialSend    = socket.SendBufferSize;

            socket.SetReceiveBufferToOSLimit();
            socket.SetSendBufferToOSLimit();

            Log.Info($"Kcp: RecvBuf = {initialReceive}=>{socket.ReceiveBufferSize} ({socket.ReceiveBufferSize/initialReceive}x) SendBuf = {initialSend}=>{socket.SendBufferSize} ({socket.SendBufferSize/initialSend}x) maximized to OS limits!");
        }
    }
}
