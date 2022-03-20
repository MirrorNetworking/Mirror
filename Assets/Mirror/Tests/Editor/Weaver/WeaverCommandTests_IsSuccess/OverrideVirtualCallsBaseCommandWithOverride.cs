using Mirror;

namespace WeaverCommandTests.OverrideVirtualCallsBaseCommandWithOverride
{
    class OverrideVirtualCallsBaseCommandWithOverride : middleBehaviour
    {
        [Command]
        protected override void CmdDoSomething()
        {
            // do something
            base.CmdDoSomething();
        }
    }


    class middleBehaviour : baseBehaviour
    {
        [Command]
        protected override void CmdDoSomething()
        {
            // do something else
            base.CmdDoSomething();
        }
    }

    class baseBehaviour : NetworkBehaviour
    {
        [Command]
        protected virtual void CmdDoSomething()
        {
            // do more stuff
        }
    }
}
