using Mirror;

namespace WeaverTargetRpcTests.ErrorWhenMethodDoesNotStartWithTarget
{
    class ErrorWhenMethodDoesNotStartWithTarget : NetworkBehaviour
    {
        [TargetRpc]
        void DoesntStartWithTarget(NetworkConnection nc) { }
    }
}
