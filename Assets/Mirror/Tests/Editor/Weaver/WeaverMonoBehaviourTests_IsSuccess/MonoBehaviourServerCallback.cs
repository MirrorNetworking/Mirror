using Mirror;
using GodotEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourServerCallback
{
    class MonoBehaviourServerCallback : MonoBehaviour
    {
        [ServerCallback]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
