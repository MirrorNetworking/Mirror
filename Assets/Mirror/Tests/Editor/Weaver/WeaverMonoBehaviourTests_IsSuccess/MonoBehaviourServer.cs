using Mirror;
using GodotEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourServer
{
    class MonoBehaviourServer : MonoBehaviour
    {
        [Server]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
