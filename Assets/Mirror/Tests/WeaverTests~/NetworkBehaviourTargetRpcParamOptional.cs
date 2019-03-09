using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(NetworkConnection monkeyCon, int monkeys = 12) {}
    }
}
