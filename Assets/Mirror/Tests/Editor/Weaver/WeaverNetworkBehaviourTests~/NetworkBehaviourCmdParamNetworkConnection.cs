using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamNetworkConnection
{
    class NetworkBehaviourCmdParamNetworkConnection : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(NetworkConnection monkeyCon) { }
    }
}
