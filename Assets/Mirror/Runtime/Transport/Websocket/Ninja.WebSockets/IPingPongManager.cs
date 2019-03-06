using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ninja.WebSockets
{
    /// <summary>
    /// Ping Pong Manager used to facilitate ping pong WebSocket messages
    /// </summary>
    interface IPingPongManager
    {
        /// <summary>
        /// Raised when a Pong frame is received
        /// </summary>
        event EventHandler<PongEventArgs> Pong;

        /// <summary>
        /// Sends a ping frame
        /// </summary>
        /// <param name="payload">The payload (must be 125 bytes of less)</param>
        /// <param name="cancellation">The cancellation token</param>
        Task SendPing(ArraySegment<byte> payload, CancellationToken cancellation);
    }
}
