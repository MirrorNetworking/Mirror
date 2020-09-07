using Mirror;

namespace WeaverClientRpcTests.ClientRpcConnCantSkipNetworkConn
{
    class ClientRpcConnCantSkipNetworkConn : NetworkBehaviour
    {
        [ClientRpc(target = Mirror.Client.Connection)]
        void ClientRpcMethod() { }
    }
}
