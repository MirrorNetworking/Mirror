using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourClientRpcParamOptional
{
    class NetworkBehaviourClientRpcParamOptional : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(int monkeys = 12) { }
    }
}
