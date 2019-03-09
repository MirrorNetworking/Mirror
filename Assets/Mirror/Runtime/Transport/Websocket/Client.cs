#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Ninja.WebSockets;
using UnityEngine;

namespace Mirror.Websocket
{

    public class Client
    {
        public event Action Connected;
        public event Action<byte[]> ReceivedData;
        public event Action Disconnected;
        public event Action<Exception> ReceivedError;

        private const int MaxMessageSize = 1024 * 256;
        WebSocket webSocket;
        CancellationTokenSource cancellation;

        public bool NoDelay = true;

        public bool Connecting { get; set; }
        public bool IsConnected { get; set; }

        private Uri uri;

        public async void Connect(Uri uri)
        {
            // not if already started
            if (webSocket != null)
            {
                // paul:  exceptions are better than silence
                ReceivedError?.Invoke(new Exception("Client already connected"));
                return;
            }
            this.uri = uri;
            // We are connecting from now until Connect succeeds or fails
            Connecting = true;

            WebSocketClientOptions options = new WebSocketClientOptions()
            {
                NoDelay = true,
                KeepAliveInterval = TimeSpan.Zero,
                SecWebSocketProtocol = "binary"
            };

            cancellation = new CancellationTokenSource();

            var clientFactory = new WebSocketClientFactory();

            try
            {
                using (webSocket = await clientFactory.ConnectAsync(uri, options, cancellation.Token))
                {
                    CancellationToken token = cancellation.Token;
                    IsConnected = true;
                    Connecting = false;
                    Connected?.Invoke();

                    await ReceiveLoop(webSocket, token);
                }
            }
            catch (ObjectDisposedException)
            {
                // No error, the client got closed
            }
            catch (Exception ex)
            {
                ReceivedError?.Invoke(ex);
            }
            finally
            {
                Disconnect();
                Disconnected?.Invoke();
            }
        }

        private async Task ReceiveLoop(WebSocket webSocket, CancellationToken token)
        {
            var buffer = new byte[MaxMessageSize];

            while (true)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);


                if (result == null)
                    break;
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                // we got a text or binary message,  need the full message
                byte[] data = await ReadFrames(result, webSocket, buffer);

                if (data == null)
                    break;

                try
                {
                    ReceivedData?.Invoke(data);
                }
                catch (Exception exception)
                {
                    ReceivedError?.Invoke(exception);
                }
            }
        }

        // a message might come splitted in multiple frames
        // collect all frames
        private async Task<byte[]> ReadFrames(WebSocketReceiveResult result, WebSocket webSocket, byte[] buffer)
        {
            int count = result.Count;

            while (!result.EndOfMessage)
            {
                if (count >= MaxMessageSize)
                {
                    string closeMessage = string.Format("Maximum message size: {0} bytes.", MaxMessageSize);
                    await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                    ReceivedError?.Invoke(new WebSocketException(WebSocketError.HeaderError));
                    return null;
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, count, MaxMessageSize - count), CancellationToken.None);
                count += result.Count;

            }
            return new ArraySegment<byte>(buffer, 0, count).ToArray();
        }

        public void Disconnect()
        {
            cancellation?.Cancel();

            // only if started
            if (webSocket != null)
            {
                // close client
                webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,"", CancellationToken.None);
                webSocket = null;
                Connecting = false;
                IsConnected = false;
            }
        }

        // send the data or throw exception
        public async void Send(byte[] data)
        {
            if (webSocket == null)
            {
                ReceivedError?.Invoke(new SocketException((int)SocketError.NotConnected));
                return;
            }

            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, cancellation.Token);
            }
            catch (Exception ex)
            {
                Disconnect();
                ReceivedError?.Invoke(ex);
            }
        }


        public override string ToString()
        {
            if (IsConnected )
            {
                return $"Websocket connected to {uri}";
            }
            if (Connecting)
            {
                return $"Websocket connecting to {uri}";
            }
            return "";
        }
    }

}

#endif
