using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourClientRpcParamNetworkConnection : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(NetworkConnection monkeyCon) {}
    }
}
