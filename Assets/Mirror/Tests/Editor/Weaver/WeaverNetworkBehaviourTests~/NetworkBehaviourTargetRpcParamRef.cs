using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamRef
{
    class NetworkBehaviourTargetRpcParamRef : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamRef(INetworkConnection monkeyCon, ref int monkeys) { }
    }
}
