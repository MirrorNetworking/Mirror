using Mirror;

namespace WeaverServerRpcTests.ServerRpcValid
{
    class ServerRpcValid : NetworkBehaviour
    {
        [ServerRpc]
        void CmdThatIsTotallyValid() { }
    }
}
