using Mirror;

namespace WeaverSyncVarHookTests.AutoDetectsOldNewHook
{
    class AutoDetectsOldNewHook : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int oldValue, int newValue)
        {

        }
    }
}
