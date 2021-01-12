using System.Collections;
using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourClientRpcCoroutine
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
