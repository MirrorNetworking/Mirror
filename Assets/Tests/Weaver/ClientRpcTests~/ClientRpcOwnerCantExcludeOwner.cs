using Mirror;

namespace ClientRpcTests.ClientRpcOwnerCantExcludeOwner
{
    class ClientRpcOwnerCantExcludeOwner : NetworkBehaviour
    {
        [ClientRpc(target = Mirror.Client.Owner, excludeOwner = true)]
        void ClientRpcMethod() { }
    }
}
