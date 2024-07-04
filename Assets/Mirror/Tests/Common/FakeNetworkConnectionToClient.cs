using System;

namespace Mirror.Tests
{
    public class FakeNetworkConnectionToClient : NetworkConnectionToClient
    {
        public FakeNetworkConnectionToClient() : base(1, "localhost") {}
        public override void Disconnect() {}
        internal override void Send(ArraySegment<byte> segment, int channelId = 0) {}
    }
}
