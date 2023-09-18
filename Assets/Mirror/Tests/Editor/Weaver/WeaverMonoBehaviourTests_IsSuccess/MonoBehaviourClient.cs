using Mirror;
using GodotEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourClient
{
    class MonoBehaviourClient : MonoBehaviour
    {
        [Client]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
