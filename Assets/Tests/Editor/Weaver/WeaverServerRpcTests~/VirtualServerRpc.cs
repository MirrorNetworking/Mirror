using Mirror;


namespace WeaverServerRpcTests.VirtualServerRpc
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
