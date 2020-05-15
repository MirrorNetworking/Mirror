using Mirror;
namespace WeaverSyncVarHookTests.FindsExplicitOldNewHookWithOtherOverloads
{
    class FindsExplicitOldNewHookWithOtherOverloads : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = SyncVarAttribute.HookParameter.OldNew)]
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
