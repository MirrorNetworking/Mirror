using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamNetworkConnection
{
    class NetworkBehaviourClientRpcParamNetworkConnection : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(INetworkConnection monkeyCon) { }
    }
}
