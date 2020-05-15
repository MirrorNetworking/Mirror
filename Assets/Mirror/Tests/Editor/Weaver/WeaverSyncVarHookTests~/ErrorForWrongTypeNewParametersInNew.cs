using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInNew
{
    class ErrorForWrongTypeNewParametersInNew : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(float newValue)
        {

        }
    }
}
