using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Ninja.WebSockets.Internal;

namespace Mirror.Websocket
{
    internal class WebsocketConnection : IConnection
    {
        private readonly WebSocket webSocket;
        const int MaxMessageSize = 256 * 1024;

        public WebsocketConnection(WebSocket webSocket)
        {
            this.webSocket = webSocket;
        }

        public void Disconnect()
        {
            var wsClient = webSocket as WebSocketImplementation;
            wsClient.Close();
        }

        public EndPoint GetEndPointAddress()
        {
            var wsClient = webSocket as WebSocketImplementation;
            return wsClient.TcpClient.Client.RemoteEndPoint;
        }
        
        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            try
            {
                buffer.Capacity = MaxMessageSize;
                buffer.SetLength(MaxMessageSize);
                buffer.Position = 0;

                buffer.TryGetBuffer(out ArraySegment<byte> segment);

                WebSocketReceiveResult result = await webSocket.ReceiveAsync(segment, CancellationToken.None);

                int count = result.Count;

                if (result.CloseStatus != null)
                    return false;

                while (!result.EndOfMessage)
                {
                    if (count >= MaxMessageSize)
                    {
                        throw new InvalidMessageException($"Websocket received message larger than {MaxMessageSize}");
                    }

                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(segment.Array, count, MaxMessageSize - count), CancellationToken.None);
                    count += result.Count;

                }
                buffer.SetLength(count);
                buffer.Position = count;

                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public Task SendAsync(ArraySegment<byte> data)
        {
            return webSocket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }
}