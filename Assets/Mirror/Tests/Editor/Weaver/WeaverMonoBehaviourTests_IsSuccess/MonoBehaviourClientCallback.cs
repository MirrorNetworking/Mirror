using Mirror;
using GodotEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourClientCallback
{
    class MonoBehaviourClientCallback : MonoBehaviour
    {
        [ClientCallback]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
