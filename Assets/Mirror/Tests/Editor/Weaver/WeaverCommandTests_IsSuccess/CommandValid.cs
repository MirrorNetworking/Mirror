using Mirror;

namespace WeaverCommandTests.CommandValid
{
    class CommandValid : NetworkBehaviour
    {
        [Command]
        void CmdThatIsTotallyValid() { }
    }
}
