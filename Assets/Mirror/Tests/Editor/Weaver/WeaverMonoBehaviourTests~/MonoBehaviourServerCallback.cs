using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMonoBehaviourTests.MonoBehaviourServerCallback
{
    class MonoBehaviourServerCallback : MonoBehaviour
    {
        [ServerCallback]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
