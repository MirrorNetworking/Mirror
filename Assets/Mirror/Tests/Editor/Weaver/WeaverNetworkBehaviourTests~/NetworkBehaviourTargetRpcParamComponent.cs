using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent
{
    class NetworkBehaviourTargetRpcParamComponent : NetworkBehaviour
    {
        public class ComponentClass : UnityEngine.Component
        {
            int monkeys = 12;
        }

        [TargetRpc]
        public void TargetRpcCantHaveParamComponent(NetworkConnection monkeyCon, ComponentClass monkeyComp) { }
    }
}
