using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMonoBehaviourTests.MonoBehaviourClient
{
    class MonoBehaviourClient : MonoBehaviour
    {
        [Client]
        void ThisCantBeOutsideNetworkBehaviour() {}
    }
}
