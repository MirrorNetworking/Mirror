using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOut
{
    class NetworkBehaviourCmdParamOut : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOut(out int monkeys)
        {
            monkeys = 12;
        }
    }
}
