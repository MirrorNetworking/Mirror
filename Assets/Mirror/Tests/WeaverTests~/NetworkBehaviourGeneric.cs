using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer<T> : NetworkBehaviour
    {
        T genericsNotAllowed;
    }
}
