using Mirror;

namespace WeaverCommandTests.OverrideVirtualCallBaseCommand
{
    class OverrideVirtualCallBaseCommand : baseBehaviour
    {
        [Command]
        protected override void CmdDoSomething()
        {
            // do somethin
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
