using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.TargetRpcStartsWithTarget
{
    class TargetRpcStartsWithTarget : NetworkBehaviour
    {
        [TargetRpc]
        void DoesntStartWithTarget(NetworkConnection nc) {}
    }
}
