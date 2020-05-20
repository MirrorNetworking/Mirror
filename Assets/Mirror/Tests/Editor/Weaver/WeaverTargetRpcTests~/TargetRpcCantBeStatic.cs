using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.TargetRpcCantBeStatic
{
    class TargetRpcCantBeStatic : NetworkBehaviour
    {
        [TargetRpc]
        static void TargetCantBeStatic(NetworkConnection nc) {}
    }
}
