using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeOldParametersInOldNewInitial
{
    class ErrorForWrongTypeOldParametersInOldNewInitial : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(float wrongOldValue, int newValue, int initialState)
        {

        }
    }
}
