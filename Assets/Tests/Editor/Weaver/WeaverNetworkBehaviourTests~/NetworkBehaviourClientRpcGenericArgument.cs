using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcGenericArgument
{
    class NetworkBehaviourClientRpcGenericArgument<T> : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveGeneric(T arg) { }
    }
}
