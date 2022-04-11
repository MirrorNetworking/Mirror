using System.Collections;
using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcCoroutine
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
