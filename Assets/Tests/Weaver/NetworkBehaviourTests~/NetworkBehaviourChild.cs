using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourChild
{
    class NetworkBehaviourChild : NetworkBehaviour
    {
        
        [ServerRpc]
        public void SendNBChild(NetworkBehaviourChild nb) {
        }
    }
}
