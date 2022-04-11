using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourServer
{
    class MonoBehaviourServer : MonoBehaviour
    {
        [Server]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
