using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOptional
{
    class NetworkBehaviourCmdParamOptional : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(int monkeys = 12) { }
    }
}
