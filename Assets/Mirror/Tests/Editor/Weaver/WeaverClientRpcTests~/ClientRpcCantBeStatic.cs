using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class ClientRpcCantBeStatic : NetworkBehaviour
    {
        [ClientRpc]
        static void RpcCantBeStatic() {}
    }
}
