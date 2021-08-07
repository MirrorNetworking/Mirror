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

        public void Connect(string host, ushort port, bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV, int timeout = DEFAULT_TIMEOUT)
        {
            Log.Info($"KcpClient: connect to {host}:{port}");

            // try resolve host name
            if (ResolveHostname(host, out IPAddress[] addresses))
            {
                // create remote endpoint
                CreateRemoteEndPoint(addresses, port);

                // create socket
                socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(remoteEndPoint);

                // set up kcp
                SetupKcp(noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize, timeout);

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
