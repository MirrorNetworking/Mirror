using Mirror;


namespace ServerRpcTests.AbstractServerRpc
{
    abstract class AbstractServerRpc : NetworkBehaviour
    {
        [ServerRpc]
        protected abstract void CmdDoSomething();
    }
}
