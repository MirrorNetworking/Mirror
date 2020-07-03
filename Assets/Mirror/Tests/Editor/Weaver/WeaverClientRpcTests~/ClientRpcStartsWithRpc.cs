using Mirror;

namespace WeaverClientRpcTests.ClientRpcStartsWithRpc
{
    class ClientRpcStartsWithRpc : NetworkBehaviour
    {
        [ClientRpc]
        void DoesntStartWithRpc() { }
    }
}
