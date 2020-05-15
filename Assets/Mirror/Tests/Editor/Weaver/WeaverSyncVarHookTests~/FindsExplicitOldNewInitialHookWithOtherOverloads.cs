using Mirror;
namespace WeaverSyncVarHookTests.FindsExplicitOldNewInitialHookWithOtherOverloads
{
    class FindsExplicitOldNewInitialHookWithOtherOverloads : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(int newValue)
        {

        }

        void onChangeHealth(int oldValue, int newValue)
        {

        }

        void onChangeHealth(int oldValue, int newValue, bool initialState)
        {

        }
    }
}
