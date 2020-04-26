using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcParamNetworkConnection : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(INetworkConnection monkeyCon) { }
    }
}
