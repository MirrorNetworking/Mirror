using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.MonoBehaviourClientCallback
{
    class MonoBehaviourClientCallback : MonoBehaviour
    {
        [ClientCallback]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
