using Mirror;
namespace WeaverSyncVarHookTests.ErrorWhenInitialParametersInOldNewInitialIsWrongType
{
    class ErrorWhenInitialParametersInOldNewInitialIsWrongType : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = SyncVarAttribute.HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(int oldValue, int newValue, int wrongInitialState)
        {

        }
    }
}
