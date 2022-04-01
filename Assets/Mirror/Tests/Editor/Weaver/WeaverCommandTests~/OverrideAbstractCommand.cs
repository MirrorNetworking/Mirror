using Mirror;


namespace WeaverCommandTests.OverrideAbstractCommand
{
    class OverrideAbstractCommand : BaseBehaviour
    {
        [Command]
        protected override void CmdDoSomething()
        {
            // do something
        }
    }

    abstract class BaseBehaviour : NetworkBehaviour
    {
        [Command]
        protected abstract void CmdDoSomething();
    }
}
