using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamOut
{
    class NetworkBehaviourClientRpcParamOut : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOut(out int monkeys)
        {
            monkeys = 12;
        }
    }
}
