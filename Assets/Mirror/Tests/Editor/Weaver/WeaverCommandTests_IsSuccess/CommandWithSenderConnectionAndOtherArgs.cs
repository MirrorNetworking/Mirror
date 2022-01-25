using Mirror;

namespace WeaverCommandTests.CommandWithSenderConnectionAndOtherArgs
{
    class CommandWithSenderConnectionAndOtherArgs : NetworkBehaviour
    {
        [Command(requiresAuthority = false)]
        void CmdFunction(int someNumber, NetworkIdentity someTarget, NetworkConnectionToClient connection = null)
        {
            // do something
        }
    }
}
