using Mirror;

namespace WeaverCommandTests.CommandThatIgnoresAuthority
{
    class CommandThatIgnoresAuthority : NetworkBehaviour
    {
        [Command(requireAuthority = false)]
        void CmdFunction()
        {
            // do something
        }
    }
}
