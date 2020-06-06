using Mirror;

namespace WeaverCommandTests.CommandThatIgnoresAuthorityWithSenderConnection
{
    class CommandThatIgnoresAuthorityWithSenderConnection : NetworkBehaviour
    {
        [Command(ignoreAuthority = true)]
        void CmdFunction(NetworkConnectionToClient connection = null)
        {
            // do something
        }
    }
}
