using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourServerRpc
{
    class MonoBehaviourServerRpc : MonoBehaviour
    {
        [ServerRpc]
        void CmdThisCantBeOutsideNetworkBehaviour() { }
    }
}
