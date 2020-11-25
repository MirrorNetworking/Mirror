using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public class KcpClientConnection : KcpConnection
    {

        readonly byte[] buffer = new byte[1500];

        public int HashCashBits {get; set;}
        /// <summary>
        /// Client connection,  does not share the UDP client with anyone
        /// so we can set up our own read loop
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public KcpClientConnection(KcpDelayMode delayMode, int sendWindowSize, int receiveWindowSize) : base(delayMode, sendWindowSize, receiveWindowSize) 
        {
        }

        internal async UniTask ConnectAsync(string host, ushort port)
        {
            IPAddress[] ipAddress = await Dns.GetHostAddressesAsync(host);
            if (ipAddress.Length < 1)
                throw new SocketException((int)SocketError.HostNotFound);

            remoteEndpoint = new IPEndPoint(ipAddress[0], port);
            socket = new Socket(remoteEndpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(remoteEndpoint);
            SetupKcp();

            ReceiveLoop().Forget();

            await HandshakeAsync(HashCashBits);
        }

        async UniTaskVoid ReceiveLoop()
        {
            try
            {
                while (socket != null)
                {
                    while (socket.Poll(0, SelectMode.SelectRead))
                    {
                        int msgLength = socket.ReceiveFrom(buffer, ref remoteEndpoint);
                        RawInput(buffer, msgLength);
                    }

                    // wait a few MS to poll again
                    await UniTask.Delay(2);
                }
            }
            catch (SocketException)
            {
                // this is fine,  the socket might have been closed in the other end
            }
        }

        protected override void Close()
        {
            socket.Close();
            socket = null;
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.Send(data, length, SocketFlags.None);
        }

        protected async UniTask HandshakeAsync(int bits)
        {
            // in the very first message we must mine a hashcash token
            // and send that as a hello
            // the server won't accept connections otherwise
            string applicationName = Application.productName;

            HashCash token = await UniTask.RunOnThreadPool( () => HashCash.Mine(applicationName, bits));
            byte[] hello = new byte[1000];
            int length = HashCashEncoding.Encode(hello, 0, token);

            var data = new ArraySegment<byte>(hello, 0, length);
            // send a greeting and see if the server replies
            await SendAsync(data);

            var stream = new MemoryStream();
            try
            {
                await ReceiveAsync(stream);
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Unable to establish connection, no Handshake message received.", e);
            }
        }
    }
}
