using Mirror;

namespace WeaverServerRpcTests.OverrideVirtualCallsBaseServerRpcWithOverride
{
    class OverrideVirtualCallsBaseServerRpcWithOverride : middleBehaviour
    {
        [ServerRpc]
        protected override void CmdDoSomething()
        {
            // do something
            base.CmdDoSomething();
        }
    }


    class middleBehaviour : baseBehaviour
    {
        [ServerRpc]
        protected override void CmdDoSomething()
        {
            // do something else
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
