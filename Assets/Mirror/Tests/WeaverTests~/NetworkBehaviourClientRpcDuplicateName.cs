using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveSameName(int abc) {}

        [ClientRpc]
        public void RpcCantHaveSameName(int abc, int def) {}
    }
}
