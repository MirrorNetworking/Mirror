using Mirror;

namespace WeaverClientRpcTests.VirtualClientRpc
{
    class VirtualCommand : NetworkBehaviour
    {
        [ClientRpc]
        protected virtual void RpcDoSomething()
        {
            // do something
        }
    }
}
