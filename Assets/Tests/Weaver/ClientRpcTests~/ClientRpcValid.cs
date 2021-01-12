using Mirror;

namespace ClientRpcTests.ClientRpcValid
{
    class ClientRpcValid : NetworkBehaviour
    {
        [ClientRpc]
        void RpcThatIsTotallyValid() { }
    }
}
