using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcParamRef : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamRef(INetworkConnection monkeyCon, ref int monkeys) { }
    }
}
