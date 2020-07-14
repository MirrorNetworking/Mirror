using Mirror;

namespace WeaverClientServerAttributeTests.NetworkBehaviourHasAuthority
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
