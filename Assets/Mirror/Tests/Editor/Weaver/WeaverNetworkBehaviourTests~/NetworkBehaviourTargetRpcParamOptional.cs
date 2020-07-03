using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamOptional
{
    class NetworkBehaviourTargetRpcParamOptional : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(INetworkConnection monkeyCon, int monkeys = 12) { }
    }
}
