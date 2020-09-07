using Mirror;

namespace WeaverServerRpcTests.ServerRpcCantBeStatic
{
    class ServerRpcCantBeStatic : NetworkBehaviour
    {
        [ServerRpc]
        static void CmdCantBeStatic() { }
    }
}
