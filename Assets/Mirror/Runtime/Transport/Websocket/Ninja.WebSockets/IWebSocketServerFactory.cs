using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ninja.WebSockets
{
    /// <summary>
    /// Web socket server factory used to open web socket server connections
    /// </summary>
    public interface IWebSocketServerFactory
    {
        /// <summary>
        /// Reads a http header information from a stream and decodes the parts relating to the WebSocket protocot upgrade
        /// </summary>
        /// <param name="stream">The network stream</param>
        /// <param name="token">The optional cancellation token</param>
        /// <returns>Http data read from the stream</returns>
        Task<WebSocketHttpContext> ReadHttpHeaderFromStreamAsync(TcpClient client, Stream stream, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Accept web socket with default options
        /// Call ReadHttpHeaderFromStreamAsync first to get WebSocketHttpContext
        /// </summary>
        /// <param name="context">The http context used to initiate this web socket request</param>
        /// <param name="token">The optional cancellation token</param>
        /// <returns>A connected web socket</returns>
        Task<WebSocket> AcceptWebSocketAsync(WebSocketHttpContext context, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Accept web socket with options specified
        /// Call ReadHttpHeaderFromStreamAsync first to get WebSocketHttpContext
        /// </summary>
        /// <param name="context">The http context used to initiate this web socket request</param>
        /// <param name="options">The web socket options</param>
        /// <param name="token">The optional cancellation token</param>
        /// <returns>A connected web socket</returns>
        Task<WebSocket> AcceptWebSocketAsync(WebSocketHttpContext context, WebSocketServerOptions options, CancellationToken token = default(CancellationToken));
    }
}
