using Mirror;
using GodotEngine;

namespace WeaverClientServerAttributeTests.MonoBehaviourServer
{
    class MonoBehaviourServer : MonoBehaviour
    {
        [Server]
        void ServerOnlyMethod() { }
    }
}
