using Mirror;
using UnityEngine;

namespace MonoBehaviourTests.MonoBehaviourServerCallback
{
    class MonoBehaviourServerCallback : MonoBehaviour
    {
        [Server(error = false)]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
