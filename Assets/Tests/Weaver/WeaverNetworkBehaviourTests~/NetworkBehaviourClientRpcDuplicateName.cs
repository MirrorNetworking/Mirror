using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcDuplicateName
{
    class NetworkBehaviourClientRpcDuplicateName : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveSameName(int abc) { }

        [ClientRpc]
        public void RpcCantHaveSameName(int abc, int def) { }
    }
}
