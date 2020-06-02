using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamAbstract
{
    class NetworkBehaviourCmdParamAbstract : NetworkBehaviour
    {
        public abstract class AbstractClass
        {
            int monkeys = 12;
        }

        [Command]
        public void CmdCantHaveParamAbstract(AbstractClass monkeys) { }
    }
}
