using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeOldParametersInOldNew
{
    class ErrorForWrongTypeOldParametersInOldNew : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNew)]
        int health;

        void onChangeHealth(float wrongOldValue, int newValue)
        {

        }
    }
}
