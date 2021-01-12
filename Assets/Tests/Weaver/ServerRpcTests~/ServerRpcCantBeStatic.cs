using Mirror;

namespace ServerRpcTests.ServerRpcCantBeStatic
{
    class ServerRpcCantBeStatic : NetworkBehaviour
    {
        [ServerRpc]
        static void CmdCantBeStatic() { }
    }
}
