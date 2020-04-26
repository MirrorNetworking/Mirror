using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.MonoBehaviourServerCallback
{
    class MonoBehaviourServerCallback : MonoBehaviour
    {
        [ServerCallback]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
