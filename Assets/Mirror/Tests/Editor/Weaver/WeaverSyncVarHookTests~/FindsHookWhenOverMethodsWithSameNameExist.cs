using Mirror;
namespace WeaverSyncVarHookTests.FindsHookWhenOverMethodsWithSameNameExist
{
    class FindsHookWhenOverMethodsWithSameNameExist : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int oldValue, int newValue)
        {

        }

        void onChangeHealth(int someOtherValue, int moreValue, bool anotherValue)
        {

        }
    }
}
