using Mirror;

namespace WeaverSyncVarTests.SyncVarsStatic
{
    class SyncVarsStatic : NetworkBehaviour
    {
        [SyncVar]
        static int invalidVar = 123;
    }
}
