using Mirror;

namespace WeaverCommandTests.CommandThatIgnoresAuthorityWithSenderConnection
{
    class CommandThatIgnoresAuthorityWithSenderConnection : NetworkBehaviour
    {
        [Command(requireAuthority = false)]
        void CmdFunction(INetworkConnection connection = null)
        {
            // do something
        }
    }
}
