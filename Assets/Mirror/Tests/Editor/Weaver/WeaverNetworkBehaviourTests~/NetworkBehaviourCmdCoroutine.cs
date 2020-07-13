using System.Collections;
using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdCoroutine
{
    class NetworkBehaviourCmdCoroutine : NetworkBehaviour
    {
        [ServerRpc]
        public IEnumerator CmdCantHaveCoroutine()
        {
            yield return null;
        }
    }
}
