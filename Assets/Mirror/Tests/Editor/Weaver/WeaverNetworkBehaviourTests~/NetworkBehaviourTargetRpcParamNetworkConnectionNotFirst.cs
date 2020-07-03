using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamNetworkConnectionNotFirst
{
    class NetworkBehaviourTargetRpcParamNetworkConnectionNotFirst : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(int abc, INetworkConnection monkeyCon) { }
    }
}
