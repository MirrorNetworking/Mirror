using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourGeneric
{
    class NetworkBehaviourGeneric<T> : NetworkBehaviour
    {
        [SyncVar]
        T genericSyncVarNotAllowed;

        T genericTypeIsFine;
    }
}
