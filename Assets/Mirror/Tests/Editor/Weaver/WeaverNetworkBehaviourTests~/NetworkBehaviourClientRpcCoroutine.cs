using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClientRpcCoroutine
{
    class NetworkBehaviourClientRpcCoroutine : NetworkBehaviour
    {
        [ClientRpc]
        public IEnumerator RpcCantHaveCoroutine()
        {
            yield return null;
        }
    }
}
