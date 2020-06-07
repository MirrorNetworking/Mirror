using Mirror;

namespace WeaverClientRpcTests.ClientRpcThatExcludesOwner
{
    class ClientRpcThatExcludesOwner : NetworkBehaviour
    {
        [ClientRpc(excludeOwner = true)]
        void RpcDoSomething()
        {
            // do something
        }
    }
}
