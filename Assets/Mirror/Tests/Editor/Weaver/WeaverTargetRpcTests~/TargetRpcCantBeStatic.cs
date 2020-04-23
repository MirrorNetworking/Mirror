using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class TargetRpcCantBeStatic : NetworkBehaviour
    {
        [TargetRpc]
        static void TargetCantBeStatic(NetworkConnection nc) {}
    }
}
