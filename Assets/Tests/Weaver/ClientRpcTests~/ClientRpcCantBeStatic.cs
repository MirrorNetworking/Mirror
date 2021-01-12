using Mirror;

namespace ClientRpcTests.ClientRpcCantBeStatic
{
    class ClientRpcCantBeStatic : NetworkBehaviour
    {
        [ClientRpc]
        static void RpcCantBeStatic() { }
    }
}
