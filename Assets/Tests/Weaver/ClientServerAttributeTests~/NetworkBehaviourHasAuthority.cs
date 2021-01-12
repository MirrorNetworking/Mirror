using Mirror;

namespace ClientServerAttributeTests.NetworkBehaviourHasAuthority
{
    class NetworkBehaviourHasAuthority : NetworkBehaviour
    {
        [HasAuthority]
        void HasAuthorityMethod()
        {
            // test method
        }
    }
}
