using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamRef
{
    class NetworkBehaviourCmdParamRef : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveParamRef(ref int monkeys) { }
    }
}
