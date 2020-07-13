using Mirror;

namespace WeaverServerRpcTests.ServerRpcWithArguments
{
    class ServerRpcWithArguments : NetworkBehaviour
    {
        [ServerRpc]
        void CmdThatIsTotallyValid(int someNumber, NetworkIdentity someTarget)
        {
            // do something
        }
    }
}
