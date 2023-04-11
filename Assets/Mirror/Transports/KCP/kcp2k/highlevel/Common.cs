using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

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
        public static void ConfigureSocketBuffers(Socket socket, int recvBufferSize, int sendBufferSize)
        {
            // log initial size for comparison.
            // remember initial size for log comparison
            int initialReceive = socket.ReceiveBufferSize;
            int initialSend    = socket.SendBufferSize;

            // set to configured size
            try
            {
                socket.ReceiveBufferSize = recvBufferSize;
                socket.SendBufferSize    = sendBufferSize;
            }
            catch (SocketException)
            {
                Log.Warning($"Kcp: failed to set Socket RecvBufSize = {recvBufferSize} SendBufSize = {sendBufferSize}");
            }


            Log.Info($"Kcp: RecvBuf = {initialReceive}=>{socket.ReceiveBufferSize} ({socket.ReceiveBufferSize/initialReceive}x) SendBuf = {initialSend}=>{socket.SendBufferSize} ({socket.SendBufferSize/initialSend}x)");
        }

        // generate a connection hash from IP+Port.
        //
        // NOTE: IPEndPoint.GetHashCode() allocates.
        //  it calls m_Address.GetHashCode().
        //  m_Address is an IPAddress.
        //  GetHashCode() allocates for IPv6:
        //  https://github.com/mono/mono/blob/bdd772531d379b4e78593587d15113c37edd4a64/mcs/class/referencesource/System/net/System/Net/IPAddress.cs#L699
        //
        // => using only newClientEP.Port wouldn't work, because
        //    different connections can have the same port.
        public static int ConnectionHash(EndPoint endPoint) =>
            endPoint.GetHashCode();

        // cookies need to be generated with a secure random generator.
        // we don't want them to be deterministic / predictable.
        // RNG is cached to avoid runtime allocations.
        static readonly RNGCryptoServiceProvider cryptoRandom = new RNGCryptoServiceProvider();
        static readonly byte[] cryptoRandomBuffer = new byte[4];
        public static uint GenerateCookie()
        {
            cryptoRandom.GetBytes(cryptoRandomBuffer);
            return BitConverter.ToUInt32(cryptoRandomBuffer, 0);
        }
    }
}
