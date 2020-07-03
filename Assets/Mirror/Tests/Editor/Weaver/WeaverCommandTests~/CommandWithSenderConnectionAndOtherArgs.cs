using Mirror;

namespace WeaverCommandTests.CommandWithSenderConnectionAndOtherArgs
{
    class CommandWithSenderConnectionAndOtherArgs : NetworkBehaviour
    {
        [Command(requireAuthority = false)]
        void CmdFunction(int someNumber, NetworkIdentity someTarget, INetworkConnection connection = null)
        {
            // do something
        }
    }
}
