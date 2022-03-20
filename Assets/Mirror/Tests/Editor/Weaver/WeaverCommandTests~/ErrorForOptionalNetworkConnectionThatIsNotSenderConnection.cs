using Mirror;

namespace WeaverCommandTests.ErrorForOptionalNetworkConnectionThatIsNotSenderConnection
{
    class ErrorForOptionalNetworkConnectionThatIsNotSenderConnection : NetworkBehaviour
    {
        [Command(requiresAuthority = false)]
        void CmdFunction(NetworkConnection connection = null)
        {
            // do something
        }
    }
}
