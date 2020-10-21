using System;
using System.IO;
using System.Net;
using Cysharp.Threading.Tasks;

namespace Mirror
{

    public static class Channel
    {
        // 2 well known channels
        // transports can implement other channels
        // to expose their features
        public const int Reliable = 0;
        public const int Unreliable = 1;
    }

    public interface IConnection
    {
        UniTask SendAsync(ArraySegment<byte> data, int channel = Channel.Reliable);

        /// <summary>
        /// reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        UniTask<(bool next, int channel)> ReceiveAsync(MemoryStream buffer);

        /// <summary>
        /// Disconnect this connection
        /// </summary>
        void Disconnect();

        /// <summary>
        /// the address of endpoint we are connected to
        /// Note this can be IPEndPoint or a custom implementation
        /// of EndPoint, which depends on the transport
        /// </summary>
        /// <returns></returns>
        EndPoint GetEndPointAddress();
    }
}
