using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MonoBehaviourServer : MonoBehaviour
    {
        [Server]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
