using Mirror;

namespace WeaverClientServerAttributeTests.NetworkBehaviourServer
{
    class NetworkBehaviourServer : NetworkBehaviour
    {
        [Server]
        void ServerOnlyMethod()
        {
            // test method
        }
    }
}
