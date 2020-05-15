using Mirror;

namespace WeaverSyncVarHookTests.AutoDetectsPrivateHook
{
    class AutoDetectsPrivateHook : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int oldValue, int newValue)
        {

        }
    }
}
