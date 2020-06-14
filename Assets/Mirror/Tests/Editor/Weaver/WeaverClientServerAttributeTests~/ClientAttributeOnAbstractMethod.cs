using Mirror;

namespace WeaverClientServerAttributeTests.ClientAttributeOnAbstractMethod
{
    abstract class ClientAttributeOnAbstractMethod : NetworkBehaviour
    {
        [Client]
        protected abstract void ClientOnlyMethod();
    }
}
