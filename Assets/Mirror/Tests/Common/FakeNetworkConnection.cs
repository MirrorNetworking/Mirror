using System;

namespace Mirror.Tests
{
    public class FakeNetworkConnection : NetworkConnectionToClient
    {
        public FakeNetworkConnection() : base(1) {}
        public override string address => "Test";
        public override void Disconnect() {}
        internal override void Send(ArraySegment<byte> segment, int channelId = 0) {}
    }
}
