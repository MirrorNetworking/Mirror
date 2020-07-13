using Mirror;


namespace WeaverServerRpcTests.AbstractServerRpc
{
    abstract class AbstractServerRpc : NetworkBehaviour
    {
        [ServerRpc]
        protected abstract void CmdDoSomething();
    }
}
