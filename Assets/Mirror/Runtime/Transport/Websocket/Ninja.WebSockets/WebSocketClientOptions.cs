using System;
using System.Collections.Generic;
using System.Text;

namespace Ninja.WebSockets
{
    /// <summary>
    /// Client WebSocket init options
    /// </summary>
    public class WebSocketClientOptions
    {
        /// <summary>
        /// How often to send ping requests to the Server
        /// This is done to prevent proxy servers from closing your connection
        /// The default is TimeSpan.Zero meaning that it is disabled.
        /// WebSocket servers usually send ping messages so it is not normally necessary for the client to send them (hence the TimeSpan.Zero default)
        /// You can manually control ping pong messages using the PingPongManager class.
        /// If you do that it is advisible to set this KeepAliveInterval to zero for the WebSocketClientFactory
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// Set to true to send a message immediately with the least amount of latency (typical usage for chat)
        /// This will disable Nagle's algorithm which can cause high tcp latency for small packets sent infrequently
        /// However, if you are streaming large packets or sending large numbers of small packets frequently it is advisable to set NoDelay to false
        /// This way data will be bundled into larger packets for better throughput
        /// </summary>
        public bool NoDelay { get; set; }

        /// <summary>
        /// Add any additional http headers to this dictionary
        /// </summary>
        public Dictionary<string,string> AdditionalHttpHeaders { get; set; }

        /// <summary>
        /// Include the full exception (with stack trace) in the close response
        /// when an exception is encountered and the WebSocket connection is closed
        /// The default is false
        /// </summary>
        public bool IncludeExceptionInCloseResponse { get; set; }

        /// <summary>
        /// WebSocket Extensions as an HTTP header value
        /// </summary>
        public string SecWebSocketExtensions { get; set; }

        /// <summary>
        /// A comma separated list of sub protocols in preference order (first one being the most preferred)
        /// The server will return the first supported sub protocol (or none if none are supported)
        /// Can be null
        /// </summary>
        public string SecWebSocketProtocol { get; set; }

        /// <summary>
        /// Initialises a new instance of the WebSocketClientOptions class
        /// </summary>
        public WebSocketClientOptions()
        {
            KeepAliveInterval = TimeSpan.Zero;
            NoDelay = true;
            AdditionalHttpHeaders = new Dictionary<string, string>();
            IncludeExceptionInCloseResponse = false;
            SecWebSocketProtocol = null;
        }
    }
}
