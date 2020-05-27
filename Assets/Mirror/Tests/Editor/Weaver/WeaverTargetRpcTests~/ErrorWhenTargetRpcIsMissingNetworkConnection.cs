using Mirror;

namespace WeaverTargetRpcTests.ErrorWhenTargetRpcIsMissingNetworkConnection
{
    class ErrorWhenTargetRpcIsMissingNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod() { }
    }
}
