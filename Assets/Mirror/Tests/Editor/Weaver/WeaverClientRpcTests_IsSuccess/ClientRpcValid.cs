using Mirror;

namespace WeaverClientRpcTests.ClientRpcValid
{
    class ClientRpcValid : NetworkBehaviour
    {
        [ClientRpc]
        void RpcThatIsTotallyValid() { }
    }
}
