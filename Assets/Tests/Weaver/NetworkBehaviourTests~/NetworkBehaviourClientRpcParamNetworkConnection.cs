using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourClientRpcParamNetworkConnection
{
    class NetworkBehaviourClientRpcParamNetworkConnection : NetworkBehaviour
    {
        [ClientRpc(target = Mirror.Client.Connection)]
        public void RpcCantHaveParamOptional(INetworkConnection monkeyCon) { }
    }
}
