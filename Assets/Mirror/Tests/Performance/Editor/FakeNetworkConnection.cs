#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
using System;

namespace Mirror.Tests.Performance
{
    public class FakeNetworkConnection : NetworkConnectionToClient
    {
        public FakeNetworkConnection(int networkConnectionId) : base(networkConnectionId, false, 0)
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
#endif
