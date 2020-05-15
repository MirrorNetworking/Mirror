using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInNew
{
    class ErrorForWrongTypeNewParametersInNew : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = SyncVarAttribute.HookParameter.New)]
        int health;

        void onChangeHealth(float wrongNewValue)
        {

        }
    }
}
