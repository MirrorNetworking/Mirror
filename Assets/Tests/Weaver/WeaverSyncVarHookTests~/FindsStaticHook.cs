using Mirror;

namespace WeaverSyncVarHookTests.FindsStaticHook
{
    class FindsStaticHook : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        static void onChangeHealth(int oldValue, int newValue)
        {

        }
    }
}
