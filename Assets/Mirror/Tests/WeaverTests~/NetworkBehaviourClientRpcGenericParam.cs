using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveGeneric<T>() {}
    }
}
