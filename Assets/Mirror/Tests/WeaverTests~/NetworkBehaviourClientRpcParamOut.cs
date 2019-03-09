using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOut(out int monkeys)
        {
            monkeys = 12;
        }
    }
}
