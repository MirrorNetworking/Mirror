using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;

namespace Mirror.KCP
{
    public class KcpServerConnection : KcpConnection
    {
        public KcpServerConnection(Socket socket, EndPoint remoteEndpoint) 
        {
            this.socket = socket;
            this.remoteEndpoint = remoteEndpoint;
            SetupKcp();
        }

        internal async UniTask HandshakeAsync()
        {
            // send a greeting and see if the server replies
            await SendAsync(Hello);
            var stream = new MemoryStream();

            // receive our first message and just throw it away
            // this first message is the one that contains the Hashcash,
            // but we don't care,  we already validated it before creating
            // the connection
            if (!await ReceiveAsync(stream))
            {
                throw new OperationCanceledException("Unable to establish connection, no Handshake message received.");
            }
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.SendTo(data, 0, length, SocketFlags.None, remoteEndpoint);
        }
    }
}
