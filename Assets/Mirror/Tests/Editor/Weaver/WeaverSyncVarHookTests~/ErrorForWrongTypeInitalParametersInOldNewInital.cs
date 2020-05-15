using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeInitalParametersInOldNewInital
{
    class ErrorForWrongTypeInitalParametersInOldNewInital : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = SyncVarAttribute.HookParameter.OldNew)]
        int health;

        void onChangeHealth(int oldValue, int newValue, int wrongInitialState)
        {

        }
    }
}
