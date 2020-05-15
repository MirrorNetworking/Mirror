using Mirror;
namespace WeaverSyncVarHookTests.ErrorWhenNoHookWithCorrectParametersAutoDetected
{
    class ErrorWhenNoHookWithCorrectParametersAutoDetected : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }
    }
}
