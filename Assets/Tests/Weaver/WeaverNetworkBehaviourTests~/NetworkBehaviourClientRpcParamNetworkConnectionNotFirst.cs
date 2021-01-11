using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamNetworkConnectionNotFirst
{
    class NetworkBehaviourClientRpcParamNetworkConnectionNotFirst : NetworkBehaviour
    {
        [ClientRpc(target = Mirror.Client.Connection)]
        public void ClientRpcCantHaveParamOptional(int abc, INetworkConnection monkeyCon) { }
    }
}
