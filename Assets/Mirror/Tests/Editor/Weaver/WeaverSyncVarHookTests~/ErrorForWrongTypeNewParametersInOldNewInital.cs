using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInOldNewInital
{
    class ErrorForWrongTypeNewParametersInOldNewInital : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = SyncVarAttribute.HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(int oldValue, float wrongNewValue, bool initialState)
        {

        }
    }
}
