using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamAbstract
{
    class NetworkBehaviourTargetRpcParamAbstract : NetworkBehaviour
    {
        public abstract class AbstractClass
        {
            int monkeys = 12;
        }

        [TargetRpc]
        public void TargetRpcCantHaveParamAbstract(NetworkConnection monkeyCon, AbstractClass monkeys) { }
    }
}
