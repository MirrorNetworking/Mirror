using Mirror;


namespace ClientRpcTests.OverrideVirtualClientRpc
{
    class OverrideVirtualClientRpc : baseBehaviour
    {
        [ClientRpc]
        protected override void RpcDoSomething()
        {
            // do something
        }
    }

    class baseBehaviour : NetworkBehaviour
    {
        [ClientRpc]
        protected virtual void RpcDoSomething()
        {

        }
    }
}
