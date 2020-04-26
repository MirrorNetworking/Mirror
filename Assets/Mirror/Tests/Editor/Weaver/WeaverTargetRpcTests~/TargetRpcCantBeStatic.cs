using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.TargetRpcCantBeStatic
{
    class TargetRpcCantBeStatic : NetworkBehaviour
    {
        [TargetRpc]
        static void TargetCantBeStatic(NetworkConnection nc) {}
    }
}
