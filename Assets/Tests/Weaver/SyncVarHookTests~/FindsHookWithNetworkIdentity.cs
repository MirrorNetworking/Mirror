using Mirror;


namespace SyncVarHookTests.FindsHookWithNetworkIdentity
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
