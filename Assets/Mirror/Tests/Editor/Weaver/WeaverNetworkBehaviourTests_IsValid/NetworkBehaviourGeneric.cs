using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourGeneric
{
    class NetworkBehaviourGeneric<T> : NetworkBehaviour
    {
        public T param;
        public readonly SyncVar<T> syncVar = new SyncVar<T>(default);
        public readonly SyncList<T> syncList = new SyncList<T>();
    }

    class GenericImplInt : NetworkBehaviourGeneric<int>
    {

    }
}
