using Mirror;
namespace WeaverSyncVarHookTests.AutoDetectsOldNewInitialHook
{
    class AutoDetectsOldNewInitialHook : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int oldValue, int newValue, bool initialState)
        {

        }
    }
}
