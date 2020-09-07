using Mirror;

namespace WeaverServerRpcTests.OverrideVirtualCallBaseServerRpc
{
    class OverrideVirtualCallBaseServerRpc : baseBehaviour
    {
        [ServerRpc]
        protected override void CmdDoSomething()
        {
            // do somethin
            base.CmdDoSomething();
        }
    }

    class baseBehaviour : NetworkBehaviour
    {
        [ServerRpc]
        protected virtual void CmdDoSomething()
        {
            // do more stuff
        }
    }
}
