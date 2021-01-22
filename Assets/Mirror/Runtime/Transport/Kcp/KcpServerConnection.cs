using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;

namespace Mirror.KCP
{
    public class KcpServerConnection : KcpConnection
    {
        internal event Action<int> DataSent;

        public KcpServerConnection(Socket socket, EndPoint remoteEndpoint, KcpDelayMode delayMode, int sendWindowSize, int receiveWindowSize) : base(delayMode, sendWindowSize, receiveWindowSize)
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

            try
            {
                // receive our first message and just throw it away
                // this first message is the one that contains the Hashcash,
                // but we don't care,  we already validated it before creating
                // the connection
                await ReceiveAsync(stream);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("No handshake received", ex);
            }
        }

        protected override void RawSend(byte[] data, int length)
        {
            DataSent?.Invoke(length);
            socket.SendTo(data, 0, length, SocketFlags.None, remoteEndpoint);
        }
    }
}
