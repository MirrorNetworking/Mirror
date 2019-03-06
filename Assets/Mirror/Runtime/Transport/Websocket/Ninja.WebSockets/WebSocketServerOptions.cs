using System;

namespace Ninja.WebSockets
{
    /// <summary>
    /// Server WebSocket init options
    /// </summary>
    public class WebSocketServerOptions
    {
        /// <summary>
        /// How often to send ping requests to the Client
        /// The default is 60 seconds
        /// This is done to prevent proxy servers from closing your connection
        /// A timespan of zero will disable the automatic ping pong mechanism
        /// You can manually control ping pong messages using the PingPongManager class.
        /// If you do that it is advisible to set this KeepAliveInterval to zero in the WebSocketServerFactory
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// Include the full exception (with stack trace) in the close response
        /// when an exception is encountered and the WebSocket connection is closed
        /// The default is false
        /// </summary>
        public bool IncludeExceptionInCloseResponse { get; set; }

        /// <summary>
        /// Specifies the sub protocol to send back to the client in the opening handshake
        /// Can be null (the most common use case)
        /// The client can specify multiple preferred protocols in the opening handshake header
        /// The server should use the first supported one or set this to null if none of the requested sub protocols are supported
        /// </summary>
        public string SubProtocol { get; set; }

        /// <summary>
        /// Initialises a new instance of the WebSocketServerOptions class
        /// </summary>
        public WebSocketServerOptions()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(60);
            IncludeExceptionInCloseResponse = false;
            SubProtocol = null;
        }
    }
}
