using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourTargetRpcParamNetworkConnection
{
    class NetworkBehaviourTargetRpcParamNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(NetworkConnection monkeyCon) {}
    }
}
