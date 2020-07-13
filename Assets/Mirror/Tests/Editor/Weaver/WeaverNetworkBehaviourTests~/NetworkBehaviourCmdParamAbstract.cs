using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamAbstract
{
    class NetworkBehaviourCmdParamAbstract : NetworkBehaviour
    {
        public abstract class AbstractClass
        {
            int monkeys = 12;
        }

        [ServerRpc]
        public void CmdCantHaveParamAbstract(AbstractClass monkeys) { }
    }
}
