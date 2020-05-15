using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInNew
{
    class ErrorForWrongTypeNewParametersInNew : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.New)]
        int health;

        void onChangeHealth(float wrongNewValue)
        {

        }
    }
}
