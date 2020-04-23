using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdParamNetworkConnection : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(NetworkConnection monkeyCon) {}
    }
}
