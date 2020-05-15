using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeOldParametersInOldNew
{
    class ErrorForWrongTypeOldParametersInOldNew : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(float oldValue, int newValue)
        {

        }
    }
}
