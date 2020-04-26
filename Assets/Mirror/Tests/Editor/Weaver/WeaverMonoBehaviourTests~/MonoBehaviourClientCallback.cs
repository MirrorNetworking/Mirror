using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMonoBehaviourTests.MonoBehaviourClientCallback
{
    class MonoBehaviourClientCallback : MonoBehaviour
    {
        [ClientCallback]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
