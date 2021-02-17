using Mirror;

namespace WeaverClientServerAttributeTests.ClientAttributeOnOverrideMethod
{
    class ClientAttributeOnOverrideMethod : BaseClass
    {
        [Client]
        protected override void ClientOnlyMethod()
        {
            // test method
        }
    }

    class BaseClass : NetworkBehaviour
    {
        protected virtual void ClientOnlyMethod()
        {
            // test method
        }
    }
}
