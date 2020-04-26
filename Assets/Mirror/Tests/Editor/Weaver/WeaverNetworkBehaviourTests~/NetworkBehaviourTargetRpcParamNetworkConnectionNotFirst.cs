using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcParamNetworkConnectionNotFirst : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(int abc, INetworkConnection monkeyCon) { }
    }
}
