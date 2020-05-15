using Mirror;
namespace WeaverSyncVarHookTests.ErrorWhenMultipleHooksAutoDetected
{
    class ErrorWhenMultipleHooksAutoDetected : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }
    }
}
