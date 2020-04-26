using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class TargetRpcStartsWithTarget : NetworkBehaviour
    {
        [TargetRpc]
        void DoesntStartWithTarget(NetworkConnection nc) {}
    }
}
