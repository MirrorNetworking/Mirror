using Mirror;


namespace WeaverCommandTests.ErrorForNetworkConnectionThatIsNotSenderConnection
{
    class ErrorForNetworkConnectionThatIsNotSenderConnection : NetworkBehaviour
    {
        [Command(ignoreAuthority = true)]
        void CmdFunction(NetworkConnection connection)
        {
            // do something
        }
    }
}
