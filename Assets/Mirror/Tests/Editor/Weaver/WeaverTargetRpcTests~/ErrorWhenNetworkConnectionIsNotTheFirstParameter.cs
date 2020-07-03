using Mirror;

namespace WeaverTargetRpcTests.ErrorWhenNetworkConnectionIsNotTheFirstParameter
{
    class ErrorWhenNetworkConnectionIsNotTheFirstParameter : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod(int potatoesRcool, NetworkConnection nc) { }
    }
}
