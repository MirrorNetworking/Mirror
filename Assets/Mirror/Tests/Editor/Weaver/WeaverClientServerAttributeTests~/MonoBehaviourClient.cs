using Mirror;
using GodotEngine;

namespace WeaverClientServerAttributeTests.MonoBehaviourClient
{
    class MonoBehaviourClient : MonoBehaviour
    {
        [Client]
        void ClientOnlyMethod() { }
    }
}
