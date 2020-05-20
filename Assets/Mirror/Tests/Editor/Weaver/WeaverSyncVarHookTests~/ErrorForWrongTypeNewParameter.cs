using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeNewParameter
{
    class ErrorForWrongTypeNewParameter : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int oldValue, float wrongNewValue)
        {

        }
    }
}
