using Mirror;


namespace WeaverTargetRpcTests.VirtualTargetRpc
{
    class VirtualTargetRpc : NetworkBehaviour
    {
        [TargetRpc]
        protected virtual void TargetDoSomething()
        {
            // do something
        }
    }
}
