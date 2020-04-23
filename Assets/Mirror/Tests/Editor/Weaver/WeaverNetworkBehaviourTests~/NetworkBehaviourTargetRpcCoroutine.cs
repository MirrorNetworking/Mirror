using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
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
