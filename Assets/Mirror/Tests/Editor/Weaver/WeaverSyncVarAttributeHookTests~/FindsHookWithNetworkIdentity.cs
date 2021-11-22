using Mirror;


namespace WeaverSyncVarHookTests.FindsHookWithNetworkIdentity
{
    class FindsHookWithNetworkIdentity : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onTargetChanged))]
        NetworkIdentity target;

        void onTargetChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
        {

        }
    }
}
