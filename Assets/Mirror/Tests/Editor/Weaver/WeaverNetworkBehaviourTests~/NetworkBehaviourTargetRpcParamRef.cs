using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcParamRef : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamRef(NetworkConnection monkeyCon, ref int monkeys) {}
    }
}
