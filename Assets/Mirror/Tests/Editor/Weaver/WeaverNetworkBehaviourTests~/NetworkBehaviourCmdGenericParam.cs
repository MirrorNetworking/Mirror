using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdGenericParam
{
    class NetworkBehaviourCmdGenericParam : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveGeneric<T>() { }
    }
}
