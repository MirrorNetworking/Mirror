using Mirror;
namespace WeaverSyncVarHookTests.AutoDetectsNewHook
{
    class AutoDetectsNewHook : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }
    }
}
