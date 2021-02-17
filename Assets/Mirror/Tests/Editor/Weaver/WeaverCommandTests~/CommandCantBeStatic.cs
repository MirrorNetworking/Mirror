using Mirror;

namespace WeaverCommandTests.CommandCantBeStatic
{
    class CommandCantBeStatic : NetworkBehaviour
    {
        [Command]
        static void CmdCantBeStatic() { }
    }
}
