using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamNetworkConnection
{
    class NetworkBehaviourTargetRpcParamNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(NetworkConnectionToClient monkeyCon) { }
    }
}
