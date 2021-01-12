using Mirror;
using UnityEngine;

namespace MonoBehaviourTests.MonoBehaviourServerRpc
{
    class MonoBehaviourServerRpc : MonoBehaviour
    {
        [ServerRpc]
        void CmdThisCantBeOutsideNetworkBehaviour() { }
    }
}
