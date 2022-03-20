using Mirror;

namespace WeaverTargetRpcTests.ErrorWhenTargetRpcIsStatic
{
    class ErrorWhenTargetRpcIsStatic : NetworkBehaviour
    {
        [TargetRpc]
        static void TargetCantBeStatic(NetworkConnection nc) { }
    }
}
