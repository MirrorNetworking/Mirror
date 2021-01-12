using Mirror;

namespace SyncVarHookTests.ErrorForWrongTypeNewParameter
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
