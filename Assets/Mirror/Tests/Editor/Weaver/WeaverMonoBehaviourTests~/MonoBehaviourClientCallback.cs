using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourClientCallback
{
    class MonoBehaviourClientCallback : MonoBehaviour
    {
        [Client(error = false)]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
