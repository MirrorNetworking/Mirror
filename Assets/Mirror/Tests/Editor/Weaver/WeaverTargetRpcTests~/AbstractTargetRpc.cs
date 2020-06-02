using Mirror;


namespace WeaverTargetRpcTests.AbstractTargetRpc
{
    abstract class AbstractTargetRpc : NetworkBehaviour
    {
        [TargetRpc]
        protected abstract void TargetDoSomething();
    }
}
