using Mirror;

namespace WeaverServerRpcTests.ServerRpcThatIgnoresAuthorityWithSenderConnection
{
    class ServerRpcThatIgnoresAuthorityWithSenderConnection : NetworkBehaviour
    {
        [ServerRpc(requireAuthority = false)]
        void CmdFunction(INetworkConnection connection = null)
        {
            // do something
        }
    }
}
