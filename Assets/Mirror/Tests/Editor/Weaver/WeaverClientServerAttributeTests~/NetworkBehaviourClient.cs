using Mirror;

namespace WeaverClientServerAttributeTests.NetworkBehaviourClient
{
    class NetworkBehaviourClient : NetworkBehaviour
    {
        [Client]
        void ClientOnlyMethod()
        {
            // test method
        }
    }
}
