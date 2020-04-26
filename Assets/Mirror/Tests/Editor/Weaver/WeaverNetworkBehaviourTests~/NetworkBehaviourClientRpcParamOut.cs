using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClientRpcParamOut
{
    class NetworkBehaviourClientRpcParamOut : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOut(out int monkeys)
        {
            monkeys = 12;
        }
    }
}
