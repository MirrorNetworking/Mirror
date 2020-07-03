using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamRef
{
    class NetworkBehaviourClientRpcParamRef : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamRef(ref int monkeys) { }
    }
}
