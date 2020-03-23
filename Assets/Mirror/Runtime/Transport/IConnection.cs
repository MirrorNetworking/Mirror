using System;
using System.IO;
using System.Threading.Tasks;

namespace Mirror
{
    public interface IConnection
    {
        Task SendAsync(ArraySegment<byte> data);

        /// <summary>
        /// reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        Task<bool> ReceiveAsync(MemoryStream buffer);

        /// <summary>
        /// Disconnect this connection
        /// </summary>
        void Disconnect();
    }
}