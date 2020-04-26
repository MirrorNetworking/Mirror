using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.ClientRpcCantBeStatic
{
    class ClientRpcCantBeStatic : NetworkBehaviour
    {
        [ClientRpc]
        static void RpcCantBeStatic() {}
    }
}
