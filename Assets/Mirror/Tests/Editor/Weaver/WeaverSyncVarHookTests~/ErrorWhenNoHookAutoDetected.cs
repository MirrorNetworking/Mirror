using Mirror;

namespace WeaverSyncVarHookTests.ErrorWhenNoHookAutoDetected
{
    class ErrorWhenNoHookAutoDetected : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }
    }
}
