using Mirror;
using GodotEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourTargetRpc
{
    class MonoBehaviourTargetRpc : MonoBehaviour
    {
        [TargetRpc]
        void TargetThisCantBeOutsideNetworkBehaviour(NetworkConnection nc) { }
    }
}
