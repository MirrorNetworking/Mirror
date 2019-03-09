using System;
using System.Collections.Generic;
using System.Text;

namespace Ninja.WebSockets
{
    /// <summary>
    /// Pong EventArgs
    /// </summary>
    public class PongEventArgs : EventArgs
    {
        /// <summary>
        /// The data extracted from a Pong WebSocket frame
        /// </summary>
        public ArraySegment<byte> Payload { get; private set; }

        /// <summary>
        /// Initialises a new instance of the PongEventArgs class
        /// </summary>
        /// <param name="payload">The pong payload must be 125 bytes or less (can be zero bytes)</param>
        public PongEventArgs(ArraySegment<byte> payload)
        {
            Payload = payload;
        }
    }
}
