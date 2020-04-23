using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
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
