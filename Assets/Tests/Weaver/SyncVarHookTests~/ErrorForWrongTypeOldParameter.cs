using Mirror;

namespace SyncVarHookTests.ErrorForWrongTypeOldParameter
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
