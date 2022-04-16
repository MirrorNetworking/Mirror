using Mirror;

namespace WeaverTargetRpcTests.TargetRpcCanHaveOtherParametersWhileSkipingNetworkConnection
{
    class TargetRpcCanHaveOtherParametersWhileSkipingNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod(int usefulNumber) { }
    }
}
