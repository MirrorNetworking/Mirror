using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.TargetRpcNetworkConnectionNotFirst
{
    class TargetRpcNetworkConnectionNotFirst : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod(int potatoesRcool, NetworkConnection nc) {}
    }
}
