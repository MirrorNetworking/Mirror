// ---------------------------------------------------------------------
// Copyright 2018 David Haig
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// ---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ninja.WebSockets.Exceptions;
using Ninja.WebSockets.Internal;

namespace Ninja.WebSockets
{
    /// <summary>
    /// Web socket server factory used to open web socket server connections
    /// </summary>
    public class WebSocketServerFactory : IWebSocketServerFactory
    {
        readonly Func<MemoryStream> _bufferFactory;
        readonly IBufferPool _bufferPool;

        /// <summary>
        /// Initialises a new instance of the WebSocketServerFactory class without caring about internal buffers
        /// </summary>
        public WebSocketServerFactory()
        {
            _bufferPool = new BufferPool();
            _bufferFactory = _bufferPool.GetBuffer;
        }

        /// <summary>
        /// Initialises a new instance of the WebSocketClientFactory class with control over internal buffer creation
        /// </summary>
        /// <param name="bufferFactory">Used to get a memory stream. Feel free to implement your own buffer pool. MemoryStreams will be disposed when no longer needed and can be returned to the pool.</param>
        public WebSocketServerFactory(Func<MemoryStream> bufferFactory)
        {
            _bufferFactory = bufferFactory;
        }

        /// <summary>
        /// Reads a http header information from a stream and decodes the parts relating to the WebSocket protocot upgrade
        /// </summary>
        /// <param name="stream">The network stream</param>
        /// <param name="token">The optional cancellation token</param>
        /// <returns>Http data read from the stream</returns>
        public async Task<WebSocketHttpContext> ReadHttpHeaderFromStreamAsync(TcpClient client, Stream stream, CancellationToken token = default(CancellationToken))
        {
            string header = await HttpHelper.ReadHttpHeaderAsync(stream, token);
            string path = HttpHelper.GetPathFromHeader(header);
            bool isWebSocketRequest = HttpHelper.IsWebSocketUpgradeRequest(header);
            IList<string> subProtocols = HttpHelper.GetSubProtocols(header);
            return new WebSocketHttpContext(isWebSocketRequest, subProtocols, header, path, client, stream);
        }

        /// <summary>
        /// Accept web socket with default options
        /// Call ReadHttpHeaderFromStreamAsync first to get WebSocketHttpContext
        /// </summary>
        /// <param name="context">The http context used to initiate this web socket request</param>
        /// <param name="token">The optional cancellation token</param>
        /// <returns>A connected web socket</returns>
        public async Task<WebSocket> AcceptWebSocketAsync(WebSocketHttpContext context, CancellationToken token = default(CancellationToken))
        {
            return await AcceptWebSocketAsync(context, new WebSocketServerOptions(), token);
        }

        /// <summary>
        /// Accept web socket with options specified
        /// Call ReadHttpHeaderFromStreamAsync first to get WebSocketHttpContext
        /// </summary>
        /// <param name="context">The http context used to initiate this web socket request</param>
        /// <param name="options">The web socket options</param>
        /// <param name="token">The optional cancellation token</param>
        /// <returns>A connected web socket</returns>
        public async Task<WebSocket> AcceptWebSocketAsync(WebSocketHttpContext context, WebSocketServerOptions options, CancellationToken token = default(CancellationToken))
        {
            Guid guid = Guid.NewGuid();
            Events.Log.AcceptWebSocketStarted(guid);
            await PerformHandshakeAsync(guid, context.HttpHeader, options.SubProtocol, context.Stream, token);
            Events.Log.ServerHandshakeSuccess(guid);
            string secWebSocketExtensions = null;
            return new WebSocketImplementation(guid, _bufferFactory, context.Stream, options.KeepAliveInterval, secWebSocketExtensions, options.IncludeExceptionInCloseResponse, false, options.SubProtocol)
            {
                Context = context
            };
        }

        static void CheckWebSocketVersion(string httpHeader)
        {
            Regex webSocketVersionRegex = new Regex("Sec-WebSocket-Version: (.*)", RegexOptions.IgnoreCase);

            // check the version. Support version 13 and above
            const int WebSocketVersion = 13;
            Match match = webSocketVersionRegex.Match(httpHeader);
            if (match.Success)
            {
                int secWebSocketVersion = Convert.ToInt32(match.Groups[1].Value.Trim());
                if (secWebSocketVersion < WebSocketVersion)
                {
                    throw new WebSocketVersionNotSupportedException(string.Format("WebSocket Version {0} not suported. Must be {1} or above", secWebSocketVersion, WebSocketVersion));
                }
            }
            else
            {
                throw new WebSocketVersionNotSupportedException("Cannot find \"Sec-WebSocket-Version\" in http header");
            }
        }

        static async Task PerformHandshakeAsync(Guid guid, String httpHeader, string subProtocol, Stream stream, CancellationToken token)
        {
            try
            {
                Regex webSocketKeyRegex = new Regex("Sec-WebSocket-Key: (.*)", RegexOptions.IgnoreCase);
                CheckWebSocketVersion(httpHeader);

                Match match = webSocketKeyRegex.Match(httpHeader);
                if (match.Success)
                {
                    string secWebSocketKey = match.Groups[1].Value.Trim();
                    string setWebSocketAccept = HttpHelper.ComputeSocketAcceptString(secWebSocketKey);
                    string response = ("HTTP/1.1 101 Switching Protocols\r\n"
                                       + "Connection: Upgrade\r\n"
                                       + "Upgrade: websocket\r\n"
                                       + (subProtocol != null ? $"Sec-WebSocket-Protocol: {subProtocol}\r\n" : "")
                                       + $"Sec-WebSocket-Accept: {setWebSocketAccept}");

                    Events.Log.SendingHandshakeResponse(guid, response);
                    await HttpHelper.WriteHttpHeaderAsync(response, stream, token);
                }
                else
                {
                    throw new SecWebSocketKeyMissingException("Unable to read \"Sec-WebSocket-Key\" from http header");
                }
            }
            catch (WebSocketVersionNotSupportedException ex)
            {
                Events.Log.WebSocketVersionNotSupported(guid, ex.ToString());
                string response = "HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: 13" + ex.Message;
                await HttpHelper.WriteHttpHeaderAsync(response, stream, token);
                throw;
            }
            catch (Exception ex)
            {
                Events.Log.BadRequest(guid, ex.ToString());
                await HttpHelper.WriteHttpHeaderAsync("HTTP/1.1 400 Bad Request", stream, token);
                throw;
            }
        }
    }
}
