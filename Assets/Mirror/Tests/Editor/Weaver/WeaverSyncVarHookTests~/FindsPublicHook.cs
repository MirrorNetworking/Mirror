using Mirror;

namespace WeaverSyncVarHookTests.FindsPublicHook
{
    class FindsPublicHook : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        public void onChangeHealth(int oldValue, int newValue)
        {

        }
    }
}
