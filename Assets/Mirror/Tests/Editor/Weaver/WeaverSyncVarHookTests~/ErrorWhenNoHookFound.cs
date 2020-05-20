using Mirror;

namespace WeaverSyncVarHookTests.ErrorWhenNoHookFound
{
    class ErrorWhenNoHookFound : NetworkBehaviour
    {
        [SyncVar(hook = "onChangeHealth")]
        int health;
    }
}
