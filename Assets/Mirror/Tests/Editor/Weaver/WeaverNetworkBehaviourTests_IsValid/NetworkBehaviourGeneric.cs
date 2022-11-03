using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourGeneric
{
    class NetworkBehaviourGeneric<T> : NetworkBehaviour
    {
        public T param;
        public readonly SyncList<T> syncList = new SyncList<T>();
    }

    class GenericImplInt : NetworkBehaviourGeneric<int>
    {

    }
}
