using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MonoBehaviourTargetRpc : MonoBehaviour
    {
        [TargetRpc]
        void TargetThisCantBeOutsideNetworkBehaviour(NetworkConnection nc) {}
    }
}
