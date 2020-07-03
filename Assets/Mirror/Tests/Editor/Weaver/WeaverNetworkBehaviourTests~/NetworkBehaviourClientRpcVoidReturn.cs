using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcVoidReturn
{
    class NetworkBehaviourClientRpcVoidReturn : NetworkBehaviour
    {
        [ClientRpc]
        public int RpcCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
