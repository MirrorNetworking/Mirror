using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourTargetRpcCoroutine
{
    class NetworkBehaviourTargetRpcCoroutine : NetworkBehaviour
    {
        [TargetRpc]
        public IEnumerator TargetRpcCantHaveCoroutine()
        {
            yield return null;
        }
    }
}
