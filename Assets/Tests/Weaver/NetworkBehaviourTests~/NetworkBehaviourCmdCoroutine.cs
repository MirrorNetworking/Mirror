using System.Collections;
using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourCmdCoroutine
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
