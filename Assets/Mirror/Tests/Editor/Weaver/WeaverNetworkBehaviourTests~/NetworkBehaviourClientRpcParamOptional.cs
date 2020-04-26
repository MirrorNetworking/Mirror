using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClientRpcParamOptional
{
    class NetworkBehaviourClientRpcParamOptional : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(int monkeys = 12) {}
    }
}
