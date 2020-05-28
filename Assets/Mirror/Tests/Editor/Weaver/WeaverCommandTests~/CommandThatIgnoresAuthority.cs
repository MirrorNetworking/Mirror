using Mirror;

namespace WeaverCommandTests.CommandThatIgnoresAuthority
{
    class CommandThatIgnoresAuthority : NetworkBehaviour
    {
        [Command(ignoreAuthority = true)]
        void CmdFunction()
        {
            // do something
        }
    }
}
