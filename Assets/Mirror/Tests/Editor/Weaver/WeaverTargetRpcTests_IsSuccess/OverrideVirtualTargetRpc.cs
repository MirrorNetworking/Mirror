using Mirror;


namespace WeaverTargetRpcTests.OverrideVirtualTargetRpc
{
    class OverrideVirtualTargetRpc : baseBehaviour
    {
        [TargetRpc]
        protected override void TargetDoSomething()
        {
            // do something
        }
    }

    class baseBehaviour : NetworkBehaviour
    {
        [TargetRpc]
        protected virtual void TargetDoSomething()
        {

        }
    }
}
