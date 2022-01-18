using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdDuplicateName
{
    class NetworkBehaviourCmdDuplicateName : NetworkBehaviour
    {
        // remote call overloads are now supported
        [Command]
        public void CmdWithSameName(int abc) { }

        [Command]
        public void CmdWithSameName(int abc, int def) { }
    }
}
