using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ninja.WebSockets.Exceptions
{
    [Serializable]
    public class WebSocketHandshakeFailedException : Exception
    {
        public WebSocketHandshakeFailedException() : base()
        {
        }

        public WebSocketHandshakeFailedException(string message) : base(message)
        {
        }

        public WebSocketHandshakeFailedException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
