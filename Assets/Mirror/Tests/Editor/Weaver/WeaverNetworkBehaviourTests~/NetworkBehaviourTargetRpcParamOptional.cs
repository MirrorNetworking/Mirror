using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcParamOptional : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(INetworkConnection monkeyCon, int monkeys = 12) { }
    }
}
