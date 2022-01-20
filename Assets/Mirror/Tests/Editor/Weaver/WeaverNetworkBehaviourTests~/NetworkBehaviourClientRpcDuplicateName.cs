using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcDuplicateName
{
    class NetworkBehaviourClientRpcDuplicateName : NetworkBehaviour
    {
        // remote call overloads are now supported
        [ClientRpc]
        public void RpcWithSameName(int abc) {}

        [ClientRpc]
        public void RpcWithSameName(int abc, int def) {}
    }
}
