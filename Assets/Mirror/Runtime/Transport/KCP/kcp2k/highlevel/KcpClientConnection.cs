using UnityEngine;
using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpClientConnection : KcpConnection
    {
        readonly byte[] buffer = new byte[1500];

        public void Connect(string host, ushort port, bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV)
        {
            Debug.Log($"KcpClient: connect to {host}:{port}");
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
                        int msgLength = socket.ReceiveFrom(buffer, ref remoteEndpoint);
                        //Debug.Log($"KCP: client raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");
                        RawInput(buffer, msgLength);
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
