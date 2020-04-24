using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class TargetRpcNetworkConnectionNotFirst : NetworkBehaviour
    {
        [TargetRpc]
        void TargetRpcMethod(int potatoesRcool, NetworkConnection nc) {}
    }
}
