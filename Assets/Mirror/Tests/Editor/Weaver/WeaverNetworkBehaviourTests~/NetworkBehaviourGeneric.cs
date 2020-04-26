using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourGeneric
{
    class NetworkBehaviourGeneric<T> : NetworkBehaviour
    {
        T genericsNotAllowed;
    }
}
