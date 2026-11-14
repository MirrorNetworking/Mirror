using System;

namespace Mirror.Tests
{
    public class FakeNetworkConnectionToClient : NetworkConnectionToClient
    {
        public FakeNetworkConnectionToClient() : base(1) {}
        public override string address => "Test";
        public override void Disconnect() {}
        internal override void Send(ArraySegment<byte> segment, int channelId = 0) {}
    }

    public class FakeNetworkConnectionToServer : NetworkConnectionToServer
    {
        public override void Disconnect() {}
        internal override void Send(ArraySegment<byte> segment, int channelId = 0) {}
    }
}
