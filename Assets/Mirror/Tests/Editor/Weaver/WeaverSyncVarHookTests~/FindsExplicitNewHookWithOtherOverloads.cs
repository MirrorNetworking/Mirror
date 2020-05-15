using Mirror;
namespace WeaverSyncVarHookTests.FindsExplicitNewHookWithOtherOverloads
{
    class FindsExplicitNewHookWithOtherOverloads : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.New)]
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
