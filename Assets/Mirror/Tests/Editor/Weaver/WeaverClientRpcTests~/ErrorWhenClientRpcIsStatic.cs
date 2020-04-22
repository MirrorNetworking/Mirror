using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class ErrorWhenClientRpcIsStatic : NetworkBehaviour
    {
        [ClientRpc]
        static void RpcCantBeStatic() {}
    }
}
