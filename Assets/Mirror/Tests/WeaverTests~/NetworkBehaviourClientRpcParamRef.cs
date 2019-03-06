using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamRef(ref int monkeys) {}
    }
}
