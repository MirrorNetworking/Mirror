using Mirror;

namespace ServerRpcTests.ServerRpcWithSenderConnectionAndOtherArgs
{
    class ServerRpcWithSenderConnectionAndOtherArgs : NetworkBehaviour
    {
        [ServerRpc(requireAuthority = false)]
        void CmdFunction(int someNumber, NetworkIdentity someTarget, INetworkConnection connection = null)
        {
            // do something
        }
    }
}
