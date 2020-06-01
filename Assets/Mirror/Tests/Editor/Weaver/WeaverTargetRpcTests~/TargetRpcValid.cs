using Mirror;

namespace WeaverTargetRpcTests.TargetRpcValid
{
    class TargetRpcValid : NetworkBehaviour
    {
        [TargetRpc]
        void TargetThatIsTotallyValid(NetworkConnection nc) { }
    }
}
