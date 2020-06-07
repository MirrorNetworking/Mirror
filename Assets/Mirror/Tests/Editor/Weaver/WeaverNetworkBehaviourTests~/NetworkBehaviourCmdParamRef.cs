using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamRef
{
    class NetworkBehaviourCmdParamRef : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamRef(ref int monkeys) { }
    }
}
