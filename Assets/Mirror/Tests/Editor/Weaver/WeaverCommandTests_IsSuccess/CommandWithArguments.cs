using Mirror;

namespace WeaverCommandTests.CommandWithArguments
{
    class CommandWithArguments : NetworkBehaviour
    {
        [Command]
        void CmdThatIsTotallyValid(int someNumber, NetworkIdentity someTarget)
        {
            // do something
        }
    }
}
