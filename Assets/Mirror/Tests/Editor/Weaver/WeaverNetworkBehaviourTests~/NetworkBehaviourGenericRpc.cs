using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourGeneric
{
    class NetworkBehaviourGeneric<T> : NetworkBehaviour
    {
        [ClientRpc]
        void RpcGeneric(T param) {}
    }
}
