using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourClientRpcParamOptional : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(int monkeys = 12) {}
    }
}
