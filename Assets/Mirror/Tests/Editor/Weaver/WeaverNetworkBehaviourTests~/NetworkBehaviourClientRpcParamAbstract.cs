using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamAbstract
{
    class NetworkBehaviourClientRpcParamAbstract : NetworkBehaviour
    {
        public abstract class AbstractClass
        {
            int monkeys = 12;
        }

        [ClientRpc]
        public void RpcCantHaveParamAbstract(AbstractClass monkeys) { }
    }
}
