using Mirror;


namespace WeaverSyncVarHookTests.AutoDetectsHookWithNetworkIdentity
{
    class AutoDetectsHookWithNetworkIdentity : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onTargetChanged))]
        NetworkIdentity target;

        void onTargetChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
        {

        }
    }
}
