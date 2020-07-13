using Mirror;

namespace WeaverServerRpcTests.OverrideVirtualCallsBaseServerRpcWithMultipleBaseClasses
{
    class OverrideVirtualCallsBaseServerRpcWithMultipleBaseClasses : middleBehaviour
    {
        [ServerRpc]
        protected override void CmdDoSomething()
        {
            // do somethin
            base.CmdDoSomething();
        }
    }

    class middleBehaviour : baseBehaviour
    {
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
