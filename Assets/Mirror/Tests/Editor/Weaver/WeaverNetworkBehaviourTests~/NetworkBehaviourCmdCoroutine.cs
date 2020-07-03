using System.Collections;
using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdCoroutine
{
    class NetworkBehaviourCmdCoroutine : NetworkBehaviour
    {
        [Command]
        public IEnumerator CmdCantHaveCoroutine()
        {
            yield return null;
        }
    }
}
