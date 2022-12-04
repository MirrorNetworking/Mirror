// test for https://github.com/vis2k/Mirror/issues/3290
using Mirror;

namespace WeaverTargetRpcTests.TargetRpcNetworkConnectionToClient
{
    class TargetRpcNetworkConnectionToClient : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcWithNetworkConnectionToClient(NetworkConnectionToClient nc) { }
    }
}
