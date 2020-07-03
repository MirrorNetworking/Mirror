using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourTargetRpc
{
    class MonoBehaviourTargetRpc : MonoBehaviour
    {
        [TargetRpc]
        void TargetThisCantBeOutsideNetworkBehaviour(NetworkConnection nc) { }
    }
}
