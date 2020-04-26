using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourTargetRpcGenericParam
{
    class NetworkBehaviourTargetRpcGenericParam : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveGeneric<T>() {}
    }
}
