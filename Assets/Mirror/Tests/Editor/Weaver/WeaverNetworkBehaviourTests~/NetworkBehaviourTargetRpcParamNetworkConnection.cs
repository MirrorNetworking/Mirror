using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcParamNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(NetworkConnection monkeyCon) {}
    }
}
