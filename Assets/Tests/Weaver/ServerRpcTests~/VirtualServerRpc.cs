using Mirror;


namespace ServerRpcTests.VirtualServerRpc
{
    class VirtualServerRpc : NetworkBehaviour
    {
        [ServerRpc]
        protected virtual void CmdDoSomething()
        {
            // do something
        }
    }
}
