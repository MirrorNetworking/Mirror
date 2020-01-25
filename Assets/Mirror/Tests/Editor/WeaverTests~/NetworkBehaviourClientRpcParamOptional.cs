using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(int monkeys = 12) {}
    }
}
