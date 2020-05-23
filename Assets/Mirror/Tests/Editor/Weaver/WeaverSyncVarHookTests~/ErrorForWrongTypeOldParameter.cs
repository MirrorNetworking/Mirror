using Mirror;

namespace WeaverSyncVarHookTests.ErrorForWrongTypeOldParameter
{
    class ErrorForWrongTypeOldParameter : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(float wrongOldValue, int newValue)
        {

        }
    }
}
