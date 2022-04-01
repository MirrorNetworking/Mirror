using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourClientRpc
{
    class MonoBehaviourClientRpc : MonoBehaviour
    {
        [ClientRpc]
        void RpcThisCantBeOutsideNetworkBehaviour() { }
    }
}
