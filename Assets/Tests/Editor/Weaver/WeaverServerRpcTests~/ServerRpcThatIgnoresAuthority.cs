using Mirror;

namespace WeaverServerRpcTests.ServerRpcThatIgnoresAuthority
{
    class ServerRpcThatIgnoresAuthority : NetworkBehaviour
    {
        [ServerRpc(requireAuthority = false)]
        void CmdFunction()
        {
            // do something
        }
    }
}
