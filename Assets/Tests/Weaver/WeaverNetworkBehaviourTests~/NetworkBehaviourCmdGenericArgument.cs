using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdGenericArgument
{
    class NetworkBehaviourCmdGenericArgument<T> : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveGeneric(T arg) { }
    }
}
