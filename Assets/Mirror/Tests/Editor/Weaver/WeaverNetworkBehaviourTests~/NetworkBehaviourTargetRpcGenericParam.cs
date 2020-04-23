using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcGenericParam : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveGeneric<T>() {}
    }
}
