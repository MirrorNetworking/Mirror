using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourTargetRpcParamRef
{
    class NetworkBehaviourTargetRpcParamRef : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamRef(NetworkConnection monkeyCon, ref int monkeys) {}
    }
}
