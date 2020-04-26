using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.TargetRpcNetworkConnectionMissing
{
    class TargetRpcNetworkConnectionMissing : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod() {}
    }
}
