using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourGeneric<T> : NetworkBehaviour
    {
        T genericsNotAllowed;
    }
}
