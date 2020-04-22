using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MonoBehaviourClient : MonoBehaviour
    {
        [Client]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
