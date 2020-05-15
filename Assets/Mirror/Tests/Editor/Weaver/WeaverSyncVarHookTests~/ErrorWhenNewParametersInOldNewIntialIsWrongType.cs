using Mirror;
namespace WeaverSyncVarHookTests.ErrorWhenNewParametersInOldNewInitialIsWrongType
{
    class ErrorWhenNewParametersInOldNewInitialIsWrongType : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = SyncVarAttribute.HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(int oldValue, float wrongNewValue, bool initialState)
        {

        }
    }
}
