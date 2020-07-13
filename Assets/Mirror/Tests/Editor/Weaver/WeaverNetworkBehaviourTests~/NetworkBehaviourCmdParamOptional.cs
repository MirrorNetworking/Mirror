using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOptional
{
    class NetworkBehaviourCmdParamOptional : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveParamOptional(int monkeys = 12) { }
    }
}
