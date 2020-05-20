using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcGenericParam
{
    class NetworkBehaviourTargetRpcGenericParam : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveGeneric<T>() {}
    }
}
