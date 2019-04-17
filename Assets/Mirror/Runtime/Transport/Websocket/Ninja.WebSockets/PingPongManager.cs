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
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Ninja.WebSockets.Internal;

namespace Ninja.WebSockets
{
    /// <summary>
    /// Ping Pong Manager used to facilitate ping pong WebSocket messages
    /// </summary>
    public class PingPongManager : IPingPongManager
    {
        readonly WebSocketImplementation _webSocket;
        readonly Guid _guid;
        readonly TimeSpan _keepAliveInterval;
        readonly Task _pingTask;
        readonly CancellationToken _cancellationToken;
        Stopwatch _stopwatch;
        long _pingSentTicks;

        /// <summary>
        /// Raised when a Pong frame is received
        /// </summary>
        public event EventHandler<PongEventArgs> Pong;

        /// <summary>
        /// Initialises a new instance of the PingPongManager to facilitate ping pong WebSocket messages.
        /// If you are manually creating an instance of this class then it is advisable to set keepAliveInterval to
        /// TimeSpan.Zero when you create the WebSocket instance (using a factory) otherwise you may be automatically
        /// be sending duplicate Ping messages (see keepAliveInterval below)
        /// </summary>
        /// <param name="webSocket">The web socket used to listen to ping messages and send pong messages</param>
        /// <param name="keepAliveInterval">The time between automatically sending ping messages.
        /// Set this to TimeSpan.Zero if you with to manually control sending ping messages.
        /// </param>
        /// <param name="cancellationToken">The token used to cancel a pending ping send AND the automatic sending of ping messages
        /// if keepAliveInterval is positive</param>
        public PingPongManager(Guid guid, WebSocket webSocket, TimeSpan keepAliveInterval, CancellationToken cancellationToken)
        {
            WebSocketImplementation webSocketImpl = webSocket as WebSocketImplementation;
            _webSocket = webSocketImpl;
            if (_webSocket == null)
                throw new InvalidCastException("Cannot cast WebSocket to an instance of WebSocketImplementation. Please use the web socket factories to create a web socket");
            _guid = guid;
            _keepAliveInterval = keepAliveInterval;
            _cancellationToken = cancellationToken;
            webSocketImpl.Pong += WebSocketImpl_Pong;
            _stopwatch = Stopwatch.StartNew();

            if (keepAliveInterval != TimeSpan.Zero)
            {
                Task.Run(PingForever, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a ping frame
        /// </summary>
        /// <param name="payload">The payload (must be 125 bytes of less)</param>
        /// <param name="cancellation">The cancellation token</param>
        public async Task SendPing(ArraySegment<byte> payload, CancellationToken cancellation)
        {
            await _webSocket.SendPingAsync(payload, cancellation);
        }

        protected virtual void OnPong(PongEventArgs e)
        {
            Pong?.Invoke(this, e);
        }

        async Task PingForever()
        {
            Events.Log.PingPongManagerStarted(_guid, (int)_keepAliveInterval.TotalSeconds);

            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_keepAliveInterval, _cancellationToken);

                    if (_webSocket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    if (_pingSentTicks != 0)
                    {
                        Events.Log.KeepAliveIntervalExpired(_guid, (int)_keepAliveInterval.TotalSeconds);
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, $"No Pong message received in response to a Ping after KeepAliveInterval {_keepAliveInterval}", _cancellationToken);
                        break;
                    }

                    if (!_cancellationToken.IsCancellationRequested)
                    {
                        _pingSentTicks = _stopwatch.Elapsed.Ticks;
                        ArraySegment<byte> buffer = new ArraySegment<byte>(BitConverter.GetBytes(_pingSentTicks));
                        await SendPing(buffer, _cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal, do nothing
            }

            Events.Log.PingPongManagerEnded(_guid);
        }

        void WebSocketImpl_Pong(object sender, PongEventArgs e)
        {
            _pingSentTicks = 0;
            OnPong(e);
        }
    }
}
