using Mirror;

namespace WeaverCommandTests.CommandThatIgnoresAuthorityWithSenderConnection
{
    class CommandThatIgnoresAuthorityWithSenderConnection : NetworkBehaviour
    {
        [Command(requiresAuthority = false)]
        void CmdFunction(NetworkConnectionToClient connection = null)
        {
            // do something
        }
    }
}
