using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcDuplicateName
{
    class NetworkBehaviourTargetRpcDuplicateName : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveSameName(INetworkConnection monkeyCon, int abc) { }

        [TargetRpc]
        public void TargetRpcCantHaveSameName(INetworkConnection monkeyCon, int abc, int def) { }
    }
}
