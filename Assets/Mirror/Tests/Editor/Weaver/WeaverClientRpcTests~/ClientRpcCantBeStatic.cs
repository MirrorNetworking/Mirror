using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverClientRpcTests.ClientRpcCantBeStatic
{
    class ClientRpcCantBeStatic : NetworkBehaviour
    {
        [ClientRpc]
        static void RpcCantBeStatic() {}
    }
}
