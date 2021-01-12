using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourCmdParamOptional
{
    class NetworkBehaviourCmdParamOptional : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveParamOptional(int monkeys = 12) { }
    }
}
