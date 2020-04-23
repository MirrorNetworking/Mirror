using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
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
