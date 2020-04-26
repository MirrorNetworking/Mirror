using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdParamNetworkConnection
{
    class NetworkBehaviourCmdParamNetworkConnection : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(NetworkConnection monkeyCon) {}
    }
}
