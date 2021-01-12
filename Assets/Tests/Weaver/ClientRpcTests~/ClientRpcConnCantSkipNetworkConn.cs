using Mirror;

namespace ClientRpcTests.ClientRpcConnCantSkipNetworkConn
{
    class ClientRpcConnCantSkipNetworkConn : NetworkBehaviour
    {
        [ClientRpc(target = Mirror.Client.Connection)]
        void ClientRpcMethod() { }
    }
}
