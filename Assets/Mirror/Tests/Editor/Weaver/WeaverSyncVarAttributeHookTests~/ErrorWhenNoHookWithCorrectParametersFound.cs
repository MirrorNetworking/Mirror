using Mirror;

namespace WeaverSyncVarHookTests.ErrorWhenNoHookWithCorrectParametersFound
{
    class ErrorWhenNoHookWithCorrectParametersFound : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int someOtherValue, int moreValue, bool anotherValue)
        {

        }

        void onChangeHealth(int someOtherValue, int moreValue, int anotherValue, int moreValues)
        {

        }
    }
}
