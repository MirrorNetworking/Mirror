using Mirror;
using GodotEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourClientRpc
{
    class MonoBehaviourClientRpc : MonoBehaviour
    {
        [ClientRpc]
        void RpcThisCantBeOutsideNetworkBehaviour() { }
    }
}
