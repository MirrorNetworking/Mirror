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
using System.Diagnostics.Tracing;
using System.Net.Security;
using System.Net.WebSockets;

namespace Ninja.WebSockets.Internal
{
    /// <summary>
    /// Use the Guid to locate this EventSource in PerfView using the Additional Providers box (without wildcard characters)
    /// </summary>
    [EventSource(Name = "Ninja-WebSockets", Guid = "7DE1A071-4F85-4DBD-8FB1-EE8D3845E087")]
    internal sealed class Events : EventSource
    {
        public static Events Log = new Events();

        [Event(1, Level = EventLevel.Informational)]
        public void ClientConnectingToIpAddress(Guid guid, string ipAddress, int port)
        {
            if (this.IsEnabled())
            {
                WriteEvent(1, guid, ipAddress, port);
            }
        }

        [Event(2, Level = EventLevel.Informational)]
        public void ClientConnectingToHost(Guid guid, string host, int port)
        {
            if (this.IsEnabled())
            {
                WriteEvent(2, guid, host, port);
            }
        }

        [Event(3, Level = EventLevel.Informational)]
        public void AttemtingToSecureSslConnection(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(3, guid);
            }
        }

        [Event(4, Level = EventLevel.Informational)]
        public void ConnectionSecured(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(4, guid);
            }
        }

        [Event(5, Level = EventLevel.Informational)]
        public void ConnectionNotSecure(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(5, guid);
            }
        }

        [Event(6, Level = EventLevel.Error)]
        public void SslCertificateError(SslPolicyErrors sslPolicyErrors)
        {
            if (this.IsEnabled())
            {
                WriteEvent(6, sslPolicyErrors);
            }
        }

        [Event(7, Level = EventLevel.Informational)]
        public void HandshakeSent(Guid guid, string httpHeader)
        {
            if (this.IsEnabled())
            {
                WriteEvent(7, guid, httpHeader ?? string.Empty);
            }
        }

        [Event(8, Level = EventLevel.Informational)]
        public void ReadingHttpResponse(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(8, guid);
            }
        }

        [Event(9, Level = EventLevel.Error)]
        public void ReadHttpResponseError(Guid guid, string exception)
        {
            if (this.IsEnabled())
            {
                WriteEvent(9, guid, exception ?? string.Empty);
            }
        }

        [Event(10, Level = EventLevel.Warning)]
        public void InvalidHttpResponseCode(Guid guid, string response)
        {
            if (this.IsEnabled())
            {
                WriteEvent(10, guid, response ?? string.Empty);
            }
        }

        [Event(11, Level = EventLevel.Error)]
        public void HandshakeFailure(Guid guid, string message)
        {
            if (this.IsEnabled())
            {
                WriteEvent(11, guid, message ?? string.Empty);
            }
        }

        [Event(12, Level = EventLevel.Informational)]
        public void ClientHandshakeSuccess(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(12, guid);
            }
        }

        [Event(13, Level = EventLevel.Informational)]
        public void ServerHandshakeSuccess(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(13, guid);
            }
        }

        [Event(14, Level = EventLevel.Informational)]
        public void AcceptWebSocketStarted(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(14, guid);
            }
        }

        [Event(15, Level = EventLevel.Informational)]
        public void SendingHandshakeResponse(Guid guid, string response)
        {
            if (this.IsEnabled())
            {
                WriteEvent(15, guid, response ?? string.Empty);
            }
        }

        [Event(16, Level = EventLevel.Error)]
        public void WebSocketVersionNotSupported(Guid guid, string exception)
        {
            if (this.IsEnabled())
            {
                WriteEvent(16, guid, exception ?? string.Empty);
            }
        }

        [Event(17, Level = EventLevel.Error)]
        public void BadRequest(Guid guid, string exception)
        {
            if (this.IsEnabled())
            {
                WriteEvent(17, guid, exception ?? string.Empty);
            }
        }

        [Event(18, Level = EventLevel.Informational)]
        public void UsePerMessageDeflate(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(18, guid);
            }
        }

        [Event(19, Level = EventLevel.Informational)]
        public void NoMessageCompression(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(19, guid);
            }
        }

        [Event(20, Level = EventLevel.Informational)]
        public void KeepAliveIntervalZero(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(20, guid);
            }
        }

        [Event(21, Level = EventLevel.Informational)]
        public void PingPongManagerStarted(Guid guid, int keepAliveIntervalSeconds)
        {
            if (this.IsEnabled())
            {
                WriteEvent(21, guid, keepAliveIntervalSeconds);
            }
        }

        [Event(22, Level = EventLevel.Informational)]
        public void PingPongManagerEnded(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(22, guid);
            }
        }

        [Event(23, Level = EventLevel.Warning)]
        public void KeepAliveIntervalExpired(Guid guid, int keepAliveIntervalSeconds)
        {
            if (this.IsEnabled())
            {
                WriteEvent(23, guid, keepAliveIntervalSeconds);
            }
        }

        [Event(24, Level = EventLevel.Warning)]
        public void CloseOutputAutoTimeout(Guid guid, WebSocketCloseStatus closeStatus, string statusDescription, string exception)
        {
            if (this.IsEnabled())
            {
                WriteEvent(24, guid, closeStatus, statusDescription ?? string.Empty, exception ?? string.Empty);
            }
        }

        [Event(25, Level = EventLevel.Error)]
        public void CloseOutputAutoTimeoutCancelled(Guid guid, int timeoutSeconds, WebSocketCloseStatus closeStatus, string statusDescription, string exception)
        {
            if (this.IsEnabled())
            {
                WriteEvent(25, guid, timeoutSeconds, closeStatus, statusDescription ?? string.Empty, exception ?? string.Empty);
            }
        }

        [Event(26, Level = EventLevel.Error)]
        public void CloseOutputAutoTimeoutError(Guid guid, string closeException, WebSocketCloseStatus closeStatus, string statusDescription, string exception)
        {
            if (this.IsEnabled())
            {
                WriteEvent(26, guid, closeException ?? string.Empty, closeStatus, statusDescription ?? string.Empty, exception ?? string.Empty);
            }
        }

        [Event(27, Level = EventLevel.Warning)]
        public void TryGetBufferNotSupported(Guid guid, string streamType)
        {
            if (this.IsEnabled())
            {
                WriteEvent(27, guid, streamType ?? string.Empty);
            }
        }

        [Event(28, Level = EventLevel.Verbose)]
        public void SendingFrame(Guid guid, WebSocketOpCode webSocketOpCode, bool isFinBitSet, int numBytes, bool isPayloadCompressed)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.None))
            {
                WriteEvent(28, guid, webSocketOpCode, isFinBitSet, numBytes, isPayloadCompressed);
            }
        }

        [Event(29, Level = EventLevel.Verbose)]
        public void ReceivedFrame(Guid guid, WebSocketOpCode webSocketOpCode, bool isFinBitSet, int numBytes)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.None))
            {
                WriteEvent(29, guid, webSocketOpCode, isFinBitSet, numBytes);
            }
        }

        [Event(30, Level = EventLevel.Informational)]
        public void CloseOutputNoHandshake(Guid guid, WebSocketCloseStatus? closeStatus, string statusDescription)
        {
            if (this.IsEnabled())
            {
                string closeStatusDesc = $"{closeStatus}";
                WriteEvent(30, guid, closeStatusDesc, statusDescription ?? string.Empty);
            }
        }

        [Event(31, Level = EventLevel.Informational)]
        public void CloseHandshakeStarted(Guid guid, WebSocketCloseStatus? closeStatus, string statusDescription)
        {
            if (this.IsEnabled())
            {
                string closeStatusDesc = $"{closeStatus}";
                WriteEvent(31, guid, closeStatusDesc, statusDescription ?? string.Empty);
            }
        }

        [Event(32, Level = EventLevel.Informational)]
        public void CloseHandshakeRespond(Guid guid, WebSocketCloseStatus? closeStatus, string statusDescription)
        {
            if (this.IsEnabled())
            {
                string closeStatusDesc = $"{closeStatus}";
                WriteEvent(32, guid, closeStatusDesc, statusDescription ?? string.Empty);
            }
        }

        [Event(33, Level = EventLevel.Informational)]
        public void CloseHandshakeComplete(Guid guid)
        {
            if (this.IsEnabled())
            {
                WriteEvent(33, guid);
            }
        }

        [Event(34, Level = EventLevel.Warning)]
        public void CloseFrameReceivedInUnexpectedState(Guid guid, WebSocketState webSocketState, WebSocketCloseStatus? closeStatus, string statusDescription)
        {
            if (this.IsEnabled())
            {
                string closeStatusDesc = $"{closeStatus}";
                WriteEvent(34, guid, webSocketState, closeStatusDesc, statusDescription ?? string.Empty);
            }
        }

        [Event(35, Level = EventLevel.Informational)]
        public void WebSocketDispose(Guid guid, WebSocketState webSocketState)
        {
            if (this.IsEnabled())
            {
                WriteEvent(35, guid, webSocketState);
            }
        }

        [Event(36, Level = EventLevel.Warning)]
        public void WebSocketDisposeCloseTimeout(Guid guid, WebSocketState webSocketState)
        {
            if (this.IsEnabled())
            {
                WriteEvent(36, guid, webSocketState);
            }
        }

        [Event(37, Level = EventLevel.Error)]
        public void WebSocketDisposeError(Guid guid, WebSocketState webSocketState, string exception)
        {
            if (this.IsEnabled())
            {
                WriteEvent(37, guid, webSocketState, exception ?? string.Empty);
            }
        }

        [Event(38, Level = EventLevel.Warning)]
        public void InvalidStateBeforeClose(Guid guid, WebSocketState webSocketState)
        {
            if (this.IsEnabled())
            {
                WriteEvent(38, guid, webSocketState);
            }
        }

        [Event(39, Level = EventLevel.Warning)]
        public void InvalidStateBeforeCloseOutput(Guid guid, WebSocketState webSocketState)
        {
            if (this.IsEnabled())
            {
                WriteEvent(39, guid, webSocketState);
            }
        }
    }
}
