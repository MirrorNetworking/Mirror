using Mirror;

namespace SyncVarTests.SyncVarsCantBeArray
{
    class SyncVarsCantBeArray : NetworkBehaviour
    {
        [SyncVar]
        int[] thisShouldntWork = new int[100];
    }
}
