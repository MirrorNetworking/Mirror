using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourCmdGenericArgument
{
    class NetworkBehaviourCmdGenericArgument<T> : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveGeneric(T arg) { }
    }
}
