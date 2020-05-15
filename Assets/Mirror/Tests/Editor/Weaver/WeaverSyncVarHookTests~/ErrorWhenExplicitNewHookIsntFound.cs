using Mirror;

namespace WeaverSyncVarHookTests.ErrorWhenExplicitNewHookIsntFound
{
    class ErrorWhenExplicitNewHookIsntFound : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth), hookParameter = HookParameter.New)]
        int health;

        void onChangeHealth(int oldValue, int newValue)
        {

        }

        void onChangeHealth(int oldValue, int newValue, bool initialState)
        {

        }
    }
}
