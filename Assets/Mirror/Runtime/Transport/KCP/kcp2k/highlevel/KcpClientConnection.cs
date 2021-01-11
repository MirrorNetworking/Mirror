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

        public void Connect(string host, ushort port, bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV)
        {
            Log.Info($"KcpClient: connect to {host}:{port}");
            IPAddress[] ipAddress = Dns.GetHostAddresses(host);
            if (ipAddress.Length < 1)
                throw new SocketException((int)SocketError.HostNotFound);

            remoteEndpoint = new IPEndPoint(ipAddress[0], port);
            socket = new Socket(remoteEndpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(remoteEndpoint);
            SetupKcp(noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize);

            // client should send handshake to server as very first message
            SendHandshake();

            RawReceive();
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
                        int msgLength = socket.ReceiveFrom(rawReceiveBuffer, ref remoteEndpoint);
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
