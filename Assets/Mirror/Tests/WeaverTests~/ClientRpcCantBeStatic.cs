using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [ClientRpc]
        private static void RpcCantBeStatic() {}
    }
}
