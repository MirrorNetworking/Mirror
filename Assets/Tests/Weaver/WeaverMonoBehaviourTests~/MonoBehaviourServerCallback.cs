using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourServerCallback
{
    class MonoBehaviourServerCallback : MonoBehaviour
    {
        [Server(error = false)]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
