using Mirror;

namespace WeaverCommandTests.ErrorWithCommandThatIgnoresAuthorityWithNetworkConnection
{
    class ErrorWithCommandThatIgnoresAuthorityWithNetworkConnection : NetworkBehaviour
    {
        [Command(ignoreAuthority = true)]
        void CmdFunction(NetworkConnection connection = null)
        {
            // do something
        }
    }
}
