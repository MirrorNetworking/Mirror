using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcDuplicateName
{
    class NetworkBehaviourTargetRpcDuplicateName : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveSameName(NetworkConnection monkeyCon, int abc) { }

        [TargetRpc]
        public void TargetRpcCantHaveSameName(NetworkConnection monkeyCon, int abc, int def) { }
    }
}
