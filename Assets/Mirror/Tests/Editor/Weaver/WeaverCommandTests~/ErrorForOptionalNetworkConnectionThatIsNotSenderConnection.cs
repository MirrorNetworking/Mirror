using Mirror;

namespace WeaverCommandTests.ErrorForOptionalNetworkConnectionThatIsNotSenderConnection
{
    class ErrorForOptionalNetworkConnectionThatIsNotSenderConnection : NetworkBehaviour
    {
        [Command(ignoreAuthority = true)]
        void CmdFunction(NetworkConnection connection = null)
        {
            // do something
        }
    }
}
