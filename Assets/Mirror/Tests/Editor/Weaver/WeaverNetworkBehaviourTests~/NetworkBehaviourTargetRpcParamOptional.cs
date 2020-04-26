using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourTargetRpcParamOptional
{
    class NetworkBehaviourTargetRpcParamOptional : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(NetworkConnection monkeyCon, int monkeys = 12) {}
    }
}
