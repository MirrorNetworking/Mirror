using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdVoidReturn
{
    class NetworkBehaviourCmdVoidReturn : NetworkBehaviour
    {
        [ServerRpc]
        public int CmdCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
