using Mirror;


namespace WeaverCommandTests.ErrorForNetworkConnectionThatIsNotSenderConnection
{
    class ErrorForNetworkConnectionThatIsNotSenderConnection : NetworkBehaviour
    {
        [Command(requiresAuthority = false)]
        void CmdFunction(NetworkConnection connection)
        {
            // do something
        }
    }
}
