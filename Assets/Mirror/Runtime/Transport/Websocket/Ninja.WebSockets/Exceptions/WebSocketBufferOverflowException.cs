using System;
using System.Collections.Generic;
using System.Text;

namespace Ninja.WebSockets.Exceptions
{
    [Serializable]
    public class WebSocketBufferOverflowException : Exception
    {
        public WebSocketBufferOverflowException() : base()
        {
        }

        public WebSocketBufferOverflowException(string message) : base(message)
        {
        }

        public WebSocketBufferOverflowException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
