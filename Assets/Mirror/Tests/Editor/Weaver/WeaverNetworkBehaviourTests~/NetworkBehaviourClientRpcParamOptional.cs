using System;
using UnityEngine;
using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamOptional
{
    class NetworkBehaviourClientRpcParamOptional : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(int monkeys = 12) {}
    }
}
