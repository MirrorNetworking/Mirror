using Mirror;


namespace WeaverCommandTests.AbstractCommand
{
    abstract class AbstractCommand : NetworkBehaviour
    {
        [Command]
        protected abstract void CmdDoSomething();
    }
}
