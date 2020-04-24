using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MonoBehaviourServerCallback : MonoBehaviour
    {
        [ServerCallback]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
