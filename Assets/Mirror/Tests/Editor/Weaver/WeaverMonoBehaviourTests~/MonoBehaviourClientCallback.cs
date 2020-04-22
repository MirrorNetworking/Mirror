using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MonoBehaviourClientCallback : MonoBehaviour
    {
        [ClientCallback]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
