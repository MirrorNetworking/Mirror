using Mirror;

namespace WeaverClientServerAttributeTests.ServerAttributeOnAbstractMethod
{
    abstract class ServerAttributeOnAbstractMethod : NetworkBehaviour
    {
        [Server]
        protected abstract void ServerOnlyMethod();
    }
}
