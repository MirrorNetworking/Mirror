#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Ninja.WebSockets;

namespace Mirror.Websocket
{

    public class Client
    {
        public event Action Connected;
        public event Action<ArraySegment<byte>> ReceivedData;
        public event Action Disconnected;
        public event Action<Exception> ReceivedError;

        const int MaxMessageSize = 1024 * 256;
        WebSocket webSocket;
        CancellationTokenSource cancellation;

        public bool NoDelay = true;

        public bool Connecting { get; set; }
        public bool IsConnected { get; set; }

        Uri uri;

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

            WebSocketClientFactory clientFactory = new WebSocketClientFactory();

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

        public bool enabled;

        async Task ReceiveLoop(WebSocket webSocket, CancellationToken token)
        {
            byte[] buffer = new byte[MaxMessageSize];

            while (true)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                await Task.Run(WaitForEnabled);

                if (result == null)
                    break;
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                // we got a text or binary message,  need the full message
                ArraySegment<byte> data = await ReadFrames(result, webSocket, buffer);

                if (data.Count == 0)
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

        void WaitForEnabled()
        {
            while (!enabled) { Task.Delay(10); }
        }

        public bool ProcessClientMessage()
        {
            // message in standalone client don't use queue to process
            return false;
        }

        // a message might come splitted in multiple frames
        // collect all frames
        async Task<ArraySegment<byte>> ReadFrames(WebSocketReceiveResult result, WebSocket webSocket, byte[] buffer)
        {
            int count = result.Count;

            while (!result.EndOfMessage)
            {
                if (count >= MaxMessageSize)
                {
                    string closeMessage = string.Format("Maximum message size: {0} bytes.", MaxMessageSize);
                    await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                    ReceivedError?.Invoke(new WebSocketException(WebSocketError.HeaderError));
                    return new ArraySegment<byte>();
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, count, MaxMessageSize - count), CancellationToken.None);
                count += result.Count;

            }
            return new ArraySegment<byte>(buffer, 0, count);
        }

        public void Disconnect()
        {
            cancellation?.Cancel();

            // only if started
            if (webSocket != null)
            {
                // close client
                webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                webSocket = null;
                Connecting = false;
                IsConnected = false;
            }
        }

        // send the data or throw exception
        public async void Send(ArraySegment<byte> segment)
        {
            if (webSocket == null)
            {
                ReceivedError?.Invoke(new SocketException((int)SocketError.NotConnected));
                return;
            }

            try
            {
                await webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, cancellation.Token);
            }
            catch (Exception ex)
            {
                Disconnect();
                ReceivedError?.Invoke(ex);
            }
        }

        public override string ToString()
        {
            if (IsConnected)
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
