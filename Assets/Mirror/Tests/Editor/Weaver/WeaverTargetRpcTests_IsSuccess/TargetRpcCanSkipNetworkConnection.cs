using Mirror;

namespace WeaverTargetRpcTests.TargetRpcCanSkipNetworkConnection
{
    class TargetRpcCanSkipNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod() { }
    }
}
