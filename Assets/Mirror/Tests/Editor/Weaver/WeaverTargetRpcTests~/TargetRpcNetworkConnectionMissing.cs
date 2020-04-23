using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class TargetRpcNetworkConnectionMissing : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod() {}
    }
}
