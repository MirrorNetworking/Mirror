using Mirror;


namespace WeaverCommandTests.VirtualCommand
{
    class VirtualCommand : NetworkBehaviour
    {
        [Command]
        protected virtual void CmdDoSomething()
        {
            // do something
        }
    }
}
