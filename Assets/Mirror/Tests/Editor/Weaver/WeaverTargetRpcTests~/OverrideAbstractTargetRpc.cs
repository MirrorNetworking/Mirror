using Mirror;


namespace WeaverTargetRpcTests.OverrideAbstractTargetRpc
{
    class OverrideAbstractTargetRpc : BaseBehaviour
    {
        [TargetRpc]
        protected override void TargetDoSomething()
        {
            // do something
        }
    }

    abstract class BaseBehaviour : NetworkBehaviour
    {
        [TargetRpc]
        protected abstract void TargetDoSomething();
    }
}
