using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourChild
{
    class NetworkBehaviourChild : NetworkBehaviour
    {
        
        [ServerRpc]
        public void SendNBChild(NetworkBehaviourChild nb) {
        }
    }
}
