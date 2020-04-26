using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourTargetRpcParamNetworkConnectionNotFirst
{
    class NetworkBehaviourTargetRpcParamNetworkConnectionNotFirst : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(int abc, NetworkConnection monkeyCon) {}
    }
}
