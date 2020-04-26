using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClientRpcParamRef
{
    class NetworkBehaviourClientRpcParamRef : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamRef(ref int monkeys) {}
    }
}
