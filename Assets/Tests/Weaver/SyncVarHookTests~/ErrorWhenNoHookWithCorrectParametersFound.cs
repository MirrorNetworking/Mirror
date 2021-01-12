using Mirror;

namespace SyncVarHookTests.ErrorWhenNoHookWithCorrectParametersFound
{
    class ErrorWhenNoHookWithCorrectParametersFound : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int someOtherValue)
        {

        }

        void onChangeHealth(int someOtherValue, int moreValue, bool anotherValue)
        {

        }
    }
}
