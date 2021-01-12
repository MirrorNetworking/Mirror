using Mirror;

namespace NetworkBehaviourTests.NetworkBehaviourGenericInherit
{
    class NetworkBehaviourGenericInherit<T> : NetworkBehaviour
    {
        protected T generic;
    }
	
	class NetworkBehaviourGenericChild : NetworkBehaviourGenericInherit<NetworkBehaviourGenericChild> { }
}
