using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdDuplicateName
{
    class NetworkBehaviourCmdDuplicateName : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveSameName(int abc) { }

        [ServerRpc]
        public void CmdCantHaveSameName(int abc, int def) { }
    }
}
