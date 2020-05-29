using Mirror;

namespace WeaverCommandTests.CommandStartsWithCmd
{
    class CommandStartsWithCmd : NetworkBehaviour
    {
        [Command]
        void DoesntStartWithCmd() { }
    }
}
