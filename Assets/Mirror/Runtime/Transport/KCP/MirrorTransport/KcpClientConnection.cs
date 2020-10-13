using UnityEngine;
using System.Net;
using System.Net.Sockets;

namespace Mirror.KCP
{
    public class KcpClientConnection : KcpConnection
    {
        readonly byte[] buffer = new byte[1500];

        /// <summary>
        /// Client connection,  does not share the UDP client with anyone
        /// so we can set up our own read loop
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public KcpClientConnection() : base()
        {
        }

        internal void Connect(string host, ushort port)
        {
            Debug.Log($"KcpClient: connect to {host}:{port}");
            IPAddress[] ipAddress = Dns.GetHostAddresses(host);
            if (ipAddress.Length < 1)
                throw new SocketException((int)SocketError.HostNotFound);

            remoteEndpoint = new IPEndPoint(ipAddress[0], port);
            socket = new Socket(remoteEndpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(remoteEndpoint);
            SetupKcp();

            RawReceive();

            Handshake();
        }

        // TODO call from transport update
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

                    // wait a few MS to poll again
                    //await UniTask.Delay(2);
                }
            }
            catch (SocketException)
            {
                // this is fine,  the socket might have been closed in the other end
            }
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
