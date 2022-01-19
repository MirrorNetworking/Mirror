using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamNetworkConnection
{
    class NetworkBehaviourTargetRpcParamNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(NetworkConnection monkeyCon) { }
    }
}
