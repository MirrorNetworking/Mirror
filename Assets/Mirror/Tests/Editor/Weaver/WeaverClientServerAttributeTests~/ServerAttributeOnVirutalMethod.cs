using Mirror;

namespace WeaverClientServerAttributeTests.ServerAttributeOnVirutalMethod
{
    class ServerAttributeOnVirutalMethod : NetworkBehaviour
    {
        [Server]
        protected virtual void ServerOnlyMethod()
        {
            // test method
        }
    }
}
