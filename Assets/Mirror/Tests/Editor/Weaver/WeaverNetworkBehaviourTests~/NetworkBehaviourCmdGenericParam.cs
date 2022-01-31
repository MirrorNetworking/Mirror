using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdGenericParam
{
    class NetworkBehaviourCmdGenericParam : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveGeneric<T>() { }
    }
}
