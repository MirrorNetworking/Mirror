using Mirror;

namespace WeaverCommandTests.OverrideVirtualCommand
{
    class OverrideVirtualCommand : baseBehaviour
    {
        [Command]
        protected override void CmdDoSomething()
        {
            // do something
        }
    }

    class baseBehaviour : NetworkBehaviour
    {
        [Command]
        protected virtual void CmdDoSomething()
        {

        }
    }
}
