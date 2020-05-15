using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInOldNew
{
    class ErrorForWrongTypeNewParametersInOldNew : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNew)]
        int health;

        void onChangeHealth(int oldValue, float wrongNewValue)
        {

        }
    }
}
