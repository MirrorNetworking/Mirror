using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourClientCallback
{
    class MonoBehaviourClientCallback : MonoBehaviour
    {
        [ClientCallback]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
