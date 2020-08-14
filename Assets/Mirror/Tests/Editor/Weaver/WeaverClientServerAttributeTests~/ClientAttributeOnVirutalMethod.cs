using Mirror;

namespace WeaverClientServerAttributeTests.ClientAttributeOnVirutalMethod
{
    class ClientAttributeOnVirutalMethod : NetworkBehaviour
    {
        [Client]
        protected virtual void ClientOnlyMethod()
        {
            // test method
        }
    }
}
