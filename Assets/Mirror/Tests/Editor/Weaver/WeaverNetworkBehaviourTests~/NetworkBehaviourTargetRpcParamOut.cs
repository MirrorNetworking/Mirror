using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamOut
{
    class NetworkBehaviourTargetRpcParamOut : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOut(NetworkConnection monkeyCon, out int monkeys)
        {
            monkeys = 12;
        }
    }
}
