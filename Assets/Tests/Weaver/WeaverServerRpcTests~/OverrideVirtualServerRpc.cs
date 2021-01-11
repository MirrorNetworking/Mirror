using Mirror;

namespace WeaverServerRpcTests.OverrideVirtualServerRpc
{
    class OverrideVirtualServerRpc : baseBehaviour
    {
        [ServerRpc]
        protected override void CmdDoSomething()
        {
            // do something
        }
    }

    class baseBehaviour : NetworkBehaviour
    {
        [ServerRpc]
        protected virtual void CmdDoSomething()
        {

        }
    }
}
