using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.TargetRpcNetworkConnectionMissing
{
    class TargetRpcNetworkConnectionMissing : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod() {}
    }
}
