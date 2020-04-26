using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.TargetRpcStartsWithTarget
{
    class TargetRpcStartsWithTarget : NetworkBehaviour
    {
        [TargetRpc]
        void DoesntStartWithTarget(NetworkConnection nc) {}
    }
}
