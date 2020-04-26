using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClientRpcParamNetworkConnection
{
    class NetworkBehaviourClientRpcParamNetworkConnection : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(NetworkConnection monkeyCon) {}
    }
}
