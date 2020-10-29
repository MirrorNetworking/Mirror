using System;

namespace Mirror.Tests
{
    public class FakeNetworkConnection : NetworkConnectionToClient
    {
        public FakeNetworkConnection() : base(1)
        {
        }

        public override string address => "Test";

        public override void Disconnect()
        {
            // nothing
        }

        internal override void Send(ArraySegment<byte> segment, int channelId = 0)
        {
        }
    }
}
