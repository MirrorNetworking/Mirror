using Mirror;

namespace WeaverCommandTests.CommandThatIgnoresAuthority
{
    class CommandThatIgnoresAuthority : NetworkBehaviour
    {
        [Command(requiresAuthority = false)]
        void CmdFunction()
        {
            // do something
        }
    }
}
