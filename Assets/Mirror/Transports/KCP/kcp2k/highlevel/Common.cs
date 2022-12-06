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
    }
}
