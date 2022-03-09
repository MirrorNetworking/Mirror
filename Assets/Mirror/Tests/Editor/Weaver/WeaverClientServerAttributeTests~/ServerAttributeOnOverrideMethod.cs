using Mirror;

namespace WeaverClientServerAttributeTests.ServerAttributeOnOverrideMethod
{
    class ServerAttributeOnOverrideMethod : BaseClass
    {
        [Server]
        protected override void ServerOnlyMethod()
        {
            // test method
        }
    }

    class BaseClass : NetworkBehaviour
    {
        protected virtual void ServerOnlyMethod()
        {
            // test method
        }
    }
}
