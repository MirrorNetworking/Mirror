using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourCmdParamOut
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
