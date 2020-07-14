using Mirror;

namespace WeaverClientServerAttributeTests.NetworkBehaviourLocalPlayer
{
    class NetworkBehaviourLocalPlayer : NetworkBehaviour
    {
        [LocalPlayer]
        void LocalPlayerMethod()
        {
            // test method
        }
    }
}
