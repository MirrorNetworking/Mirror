using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamNetworkConnectionNotFirst
{
    class NetworkBehaviourTargetRpcParamNetworkConnectionNotFirst : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(int abc, NetworkConnection monkeyCon) { }
    }
}
