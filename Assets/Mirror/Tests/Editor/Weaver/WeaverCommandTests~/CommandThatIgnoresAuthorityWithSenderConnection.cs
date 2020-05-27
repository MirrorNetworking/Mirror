using Mirror;

namespace WeaverCommandTests.CommandThatIgnoresAuthorityWithSenderConnection
{
    class CommandThatIgnoresAuthorityWithSenderConnection : NetworkBehaviour
    {
        [Command(ignoreAuthority = true)]
        void CmdFunction([SenderConnection] NetworkConnection connection = null)
        {
            // do something
        }
    }
}
