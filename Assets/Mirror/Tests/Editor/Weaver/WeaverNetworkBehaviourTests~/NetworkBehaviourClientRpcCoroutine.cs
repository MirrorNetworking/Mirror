using System.Collections;
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
