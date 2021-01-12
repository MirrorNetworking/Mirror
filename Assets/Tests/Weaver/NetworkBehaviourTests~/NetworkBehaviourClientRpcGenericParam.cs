using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourClientRpcGenericParam
{
    class NetworkBehaviourClientRpcGenericParam : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveGeneric<T>() { }
    }
}
