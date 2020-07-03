using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdVoidReturn
{
    class NetworkBehaviourCmdVoidReturn : NetworkBehaviour
    {
        [Command]
        public int CmdCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
