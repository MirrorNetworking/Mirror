using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpClientConnection : KcpConnection
    {
        // IMPORTANT: raw receive buffer always needs to be of 'MTU' size, even
        //            if MaxMessageSize is larger. kcp always sends in MTU
        //            segments and having a buffer smaller than MTU would
        //            silently drop excess data.
        //            => we need the MTU to fit channel + message!
        readonly byte[] rawReceiveBuffer = new byte[Kcp.MTU_DEF];

        // helper function to resolve host to IPAddress
        public static bool ResolveHostname(string hostname, out IPAddress[] addresses)
        {
            try
            {
                // NOTE: dns lookup is blocking. this can take a second.
                addresses = Dns.GetHostAddresses(hostname);
                return addresses.Length >= 1;
            }
            catch (SocketException)
            {
                Log.Info($"Failed to resolve host: {hostname}");
                addresses = null;
                return false;
            }
        }

        // EndPoint & Receive functions can be overwritten for where-allocation:
        // https://github.com/vis2k/where-allocation
        // NOTE: Client's SendTo doesn't allocate, don't need a virtual.
        protected virtual void CreateRemoteEndPoint(IPAddress[] addresses, ushort port) =>
            remoteEndPoint = new IPEndPoint(addresses[0], port);

        protected virtual int ReceiveFrom(byte[] buffer) =>
            socket.ReceiveFrom(buffer, ref remoteEndPoint);

        // if connections drop under heavy load, increase to OS limit.
        // if still not enough, increase the OS limit.
        void ConfigureSocketBufferSizes(bool maximizeSendReceiveBuffersToOSLimit)
        {
            if (maximizeSendReceiveBuffersToOSLimit)
            {
                // log initial size for comparison.
                // remember initial size for log comparison
                int initialReceive = socket.ReceiveBufferSize;
                int initialSend = socket.SendBufferSize;

                socket.SetReceiveBufferToOSLimit();
                socket.SetSendBufferToOSLimit();
                Log.Info($"KcpClient: RecvBuf = {initialReceive}=>{socket.ReceiveBufferSize} ({socket.ReceiveBufferSize/initialReceive}x) SendBuf = {initialSend}=>{socket.SendBufferSize} ({socket.SendBufferSize/initialSend}x) increased to OS limits!");
            }
            // otherwise still log the defaults for info.
            else Log.Info($"KcpClient: RecvBuf = {socket.ReceiveBufferSize} SendBuf = {socket.SendBufferSize}. If connections drop under heavy load, enable {nameof(maximizeSendReceiveBuffersToOSLimit)} to increase it to OS limit. If they still drop, increase the OS limit.");
        }

        public void Connect(string host,
                            ushort port,
                            bool noDelay,
                            uint interval = Kcp.INTERVAL,
                            int fastResend = 0,
                            bool congestionWindow = true,
                            uint sendWindowSize = Kcp.WND_SND,
                            uint receiveWindowSize = Kcp.WND_RCV,
                            int timeout = DEFAULT_TIMEOUT,
                            uint maxRetransmits = Kcp.DEADLINK,
                            bool maximizeSendReceiveBuffersToOSLimit = false)
        {
            Log.Info($"KcpClient: connect to {host}:{port}");

            // try resolve host name
            if (ResolveHostname(host, out IPAddress[] addresses))
            {
                // create remote endpoint
                CreateRemoteEndPoint(addresses, port);

                // create socket
                socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                // configure buffer sizes
                ConfigureSocketBufferSizes(maximizeSendReceiveBuffersToOSLimit);

                // connect
                socket.Connect(remoteEndPoint);

                // set up kcp
                SetupKcp(noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize, timeout, maxRetransmits);

                // client should send handshake to server as very first message
                SendHandshake();

                RawReceive();
            }
            // otherwise call OnDisconnected to let the user know.
            else OnDisconnected();
        }

        // call from transport update
        public void RawReceive()
        {
            try
            {
                if (socket != null)
                {
                    while (socket.Poll(0, SelectMode.SelectRead))
                    {
                        int msgLength = ReceiveFrom(rawReceiveBuffer);
                        // IMPORTANT: detect if buffer was too small for the
                        //            received msgLength. otherwise the excess
                        //            data would be silently lost.
                        //            (see ReceiveFrom documentation)
                        if (msgLength <= rawReceiveBuffer.Length)
                        {
                            //Log.Debug($"KCP: client raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");
                            RawInput(rawReceiveBuffer, msgLength);
                        }
                        else
                        {
                            Log.Error($"KCP ClientConnection: message of size {msgLength} does not fit into buffer of size {rawReceiveBuffer.Length}. The excess was silently dropped. Disconnecting.");
                            Disconnect();
                        }
                    }
                }
            }
            // this is fine, the socket might have been closed in the other end
            catch (SocketException) {}
        }

        protected override void Dispose()
        {
            socket.Close();
            socket = null;
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.Send(data, length, SocketFlags.None);
        }
    }
}
