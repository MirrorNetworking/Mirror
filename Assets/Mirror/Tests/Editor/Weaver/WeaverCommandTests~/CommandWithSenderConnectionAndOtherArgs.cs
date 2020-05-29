using Mirror;

namespace WeaverCommandTests.CommandWithSenderConnectionAndOtherArgs
{
    class CommandWithSenderConnectionAndOtherArgs : NetworkBehaviour
    {
        [Command(ignoreAuthority = true)]
        void CmdFunction(int someNumber, NetworkIdentity someTarget, [SenderConnection] NetworkConnection connection = null)
        {
            // do something
        }
    }
}
