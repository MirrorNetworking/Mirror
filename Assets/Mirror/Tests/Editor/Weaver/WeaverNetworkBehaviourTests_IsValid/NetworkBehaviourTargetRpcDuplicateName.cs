using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcDuplicateName
{
    class NetworkBehaviourTargetRpcDuplicateName : NetworkBehaviour
    {
        // remote call overloads are now supported
        [TargetRpc]
        public void TargetRpcWithSameName(NetworkConnectionToClient monkeyCon, int abc) { }

        [TargetRpc]
        public void TargetRpcWithSameName(NetworkConnectionToClient monkeyCon, int abc, int def) { }
    }
}
