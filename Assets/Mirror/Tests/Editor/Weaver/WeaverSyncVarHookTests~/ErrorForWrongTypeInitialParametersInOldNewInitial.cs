using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeInitialParametersInOldNewInitial
{
    class ErrorForWrongTypeInitialParametersInOldNewInitial : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(int oldValue, int newValue, int wrongInitialState)
        {

        }
    }
}
