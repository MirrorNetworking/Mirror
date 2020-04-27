using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.TargetRpcNetworkConnectionNotFirst
{
    class TargetRpcNetworkConnectionNotFirst : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod(int potatoesRcool, NetworkConnection nc) {}
    }
}
