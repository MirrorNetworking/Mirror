using Mirror;


namespace ClientRpcTests.OverrideAbstractClientRpc
{
    class OverrideAbstractClientRpc : BaseBehaviour
    {
        [ClientRpc]
        protected override void RpcDoSomething()
        {
            // do something
        }
    }

    abstract class BaseBehaviour : NetworkBehaviour
    {
        [ClientRpc]
        protected abstract void RpcDoSomething();
    }
}
