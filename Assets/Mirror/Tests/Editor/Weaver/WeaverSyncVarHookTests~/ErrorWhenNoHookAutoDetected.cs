using Mirror;

namespace WeaverSyncVarHookTests.ErrorWhenNoHookAutoDetected
{
    class ErrorWhenNoHookAutoDetected : NetworkBehaviour
    {
        [SyncVar(hook = "onChangeHealth")]
        int health;
    }
}
