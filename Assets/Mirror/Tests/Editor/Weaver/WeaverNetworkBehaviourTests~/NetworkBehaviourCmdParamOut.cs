using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOut
{
    class NetworkBehaviourCmdParamOut : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveParamOut(out int monkeys)
        {
            monkeys = 12;
        }
    }
}
