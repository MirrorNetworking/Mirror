using Mirror;

namespace WeaverClientRpcTests.AbstractClientRpc
{
    abstract class AbstractClientRpc : NetworkBehaviour
    {
        [ClientRpc]
        protected abstract void RpcDoSomething();
    }
}
