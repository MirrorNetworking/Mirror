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

        void onChangeHealth(int oldValue, int newValue)
        {

        }

        void onChangeHealth(int oldValue, int newValue, bool initialState)
        {

        }
    }
}
