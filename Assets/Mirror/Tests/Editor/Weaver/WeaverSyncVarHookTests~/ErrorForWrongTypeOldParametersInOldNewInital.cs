using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeOldParametersInOldNewInital
{
    class ErrorForWrongTypeOldParametersInOldNewInital : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = SyncVarAttribute.HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(float wrongOldValue, int newValue, int initialState)
        {

        }
    }
}
