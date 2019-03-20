using System.Net.WebSockets;

namespace Ninja.WebSockets.Internal
{
    internal class WebSocketFrame
    {
        public bool IsFinBitSet { get; private set; }

        public WebSocketOpCode OpCode { get; private set; }

        public int Count { get; private set; }

        public WebSocketCloseStatus? CloseStatus { get; private set; }

        public string CloseStatusDescription { get; private set; }

        public WebSocketFrame(bool isFinBitSet, WebSocketOpCode webSocketOpCode, int count)
        {
            IsFinBitSet = isFinBitSet;
            OpCode = webSocketOpCode;
            Count = count;
        }

        public WebSocketFrame(bool isFinBitSet, WebSocketOpCode webSocketOpCode, int count, WebSocketCloseStatus closeStatus, string closeStatusDescription) : this(isFinBitSet, webSocketOpCode, count)
        {
            CloseStatus = closeStatus;
            CloseStatusDescription = closeStatusDescription;
        }

    }
}
