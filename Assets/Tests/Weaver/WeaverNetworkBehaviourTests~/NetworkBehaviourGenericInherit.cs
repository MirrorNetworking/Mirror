using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourGenericInherit
{
    class NetworkBehaviourGenericInherit<T> : NetworkBehaviour
    {
        protected T generic;
    }
	
	class NetworkBehaviourGenericChild : NetworkBehaviourGenericInherit<NetworkBehaviourGenericChild> { }
}
