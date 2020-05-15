using Mirror;

namespace WeaverSyncVarHookTests.ErrorWhenExplicitOldNewHookIsntFound
{
    class ErrorWhenExplicitOldNewHookIsntFound : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNew)]
        int health;

        void onChangeHealth(int newValue)
        {

        }

        void onChangeHealth(int oldValue, int newValue, bool initialState)
        {

        }
    }
}
