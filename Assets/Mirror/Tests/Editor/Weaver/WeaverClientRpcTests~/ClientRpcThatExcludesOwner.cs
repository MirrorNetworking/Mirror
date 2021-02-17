using Mirror;

namespace WeaverClientRpcTests.ClientRpcThatExcludesOwner
{
    class ClientRpcThatExcludesOwner : NetworkBehaviour
    {
        [ClientRpc(includeOwner = false)]
        void RpcDoSomething()
        {
            // do something
        }
    }
}
