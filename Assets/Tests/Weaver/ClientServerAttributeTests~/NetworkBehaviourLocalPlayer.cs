using Mirror;

namespace ClientServerAttributeTests.NetworkBehaviourLocalPlayer
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
