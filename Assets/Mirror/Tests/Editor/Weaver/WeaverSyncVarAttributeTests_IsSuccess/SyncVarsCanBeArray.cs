using Mirror;

namespace WeaverSyncVarTests.SyncVarsCantBeArray
{
    class SyncVarsCanBeArray : NetworkBehaviour
    {
        [SyncVar]
        int[] thisShouldWork = new int[100];
    }
}
