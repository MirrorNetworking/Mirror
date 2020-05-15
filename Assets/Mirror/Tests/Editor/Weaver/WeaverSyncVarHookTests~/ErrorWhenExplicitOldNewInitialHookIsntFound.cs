using Mirror;

namespace WeaverSyncVarHookTests.ErrorWhenExplicitOldNewInitialHookIsntFound
{
    class ErrorWhenExplicitOldNewInitialHookIsntFound : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.OldNewInitial)]
        int health;

        void onChangeHealth(int newValue)
        {

        }

        void onChangeHealth(int oldValue, int newValue)
        {

        }
    }
}
