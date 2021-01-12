using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourCmdParamRef
{
    class NetworkBehaviourCmdParamRef : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveParamRef(ref int monkeys) { }
    }
}
