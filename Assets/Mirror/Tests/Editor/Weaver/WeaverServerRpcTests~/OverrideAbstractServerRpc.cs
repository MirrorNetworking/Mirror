using Mirror;


namespace WeaverServerRpcTests.OverrideAbstractServerRpc
{
    class OverrideAbstractServerRpc : BaseBehaviour
    {
        [ServerRpc]
        protected override void CmdDoSomething()
        {
            // do something
        }
    }

    abstract class BaseBehaviour : NetworkBehaviour
    {
        [ServerRpc]
        protected abstract void CmdDoSomething();
    }
}
