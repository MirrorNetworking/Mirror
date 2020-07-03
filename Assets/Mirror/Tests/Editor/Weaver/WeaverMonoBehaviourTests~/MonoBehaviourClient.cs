using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourClient
{
    class MonoBehaviourClient : MonoBehaviour
    {
        [Client]
        void ThisCantBeOutsideNetworkBehaviour() { }
    }
}
