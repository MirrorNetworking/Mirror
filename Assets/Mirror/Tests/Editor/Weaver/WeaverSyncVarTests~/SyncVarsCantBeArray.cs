using Mirror;

namespace WeaverSyncVarTests.SyncVarsCantBeArray
{
    class SyncVarsCantBeArray : NetworkBehaviour
    {
        [SyncVar]
        int[] thisShouldntWork = new int[100];
    }
}
