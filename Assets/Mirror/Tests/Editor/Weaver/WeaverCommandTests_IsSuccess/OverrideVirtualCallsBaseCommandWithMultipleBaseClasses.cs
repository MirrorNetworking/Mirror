using Mirror;

namespace WeaverCommandTests.OverrideVirtualCallsBaseCommandWithMultipleBaseClasses
{
    class OverrideVirtualCallsBaseCommandWithMultipleBaseClasses : middleBehaviour
    {
        [Command]
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
        [Command]
        protected virtual void CmdDoSomething()
        {
            // do more stuff
        }
    }
}
