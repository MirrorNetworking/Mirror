using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamOptional
{
    class NetworkBehaviourTargetRpcParamOptional : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(NetworkConnection monkeyCon, int monkeys = 12) { }
    }
}
