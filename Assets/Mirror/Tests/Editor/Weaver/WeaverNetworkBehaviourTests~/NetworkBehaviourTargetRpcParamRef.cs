using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamRef
{
    class NetworkBehaviourTargetRpcParamRef : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamRef(NetworkConnection monkeyCon, ref int monkeys) { }
    }
}
