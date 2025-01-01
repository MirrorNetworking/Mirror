using Mirror;

namespace WeaverSyncVarTests.SyncVarsCanBeArray
{
    class SyncVarsCanBeArray : NetworkBehaviour
    {
        [SyncVar]
        int[] thisShouldWork = new int[100];
    }
}
