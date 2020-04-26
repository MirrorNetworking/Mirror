using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClientRpcGenericParam
{
    class NetworkBehaviourClientRpcGenericParam : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveGeneric<T>() {}
    }
}
