using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.MonoBehaviourTargetRpc
{
    class MonoBehaviourTargetRpc : MonoBehaviour
    {
        [TargetRpc]
        void TargetThisCantBeOutsideNetworkBehaviour(NetworkConnection nc) {}
    }
}
