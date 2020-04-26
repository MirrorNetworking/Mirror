using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourTargetRpcParamOut
{
    class NetworkBehaviourTargetRpcParamOut : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOut(NetworkConnection monkeyCon, out int monkeys)
        {
            monkeys = 12;
        }
    }
}
