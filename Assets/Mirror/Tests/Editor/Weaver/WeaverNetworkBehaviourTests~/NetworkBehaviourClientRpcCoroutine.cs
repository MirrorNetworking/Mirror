using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcCoroutine
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
