using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcGenericParam
{
    class NetworkBehaviourClientRpcGenericParam : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveGeneric<T>() { }
    }
}
