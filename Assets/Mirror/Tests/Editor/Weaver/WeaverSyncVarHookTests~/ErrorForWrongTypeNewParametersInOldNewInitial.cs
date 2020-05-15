using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInOldNewInitial
{
    class ErrorForWrongTypeNewParametersInOldNewInitial : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(int oldValue, float wrongNewValue, bool initialState)
        {

        }
    }
}
