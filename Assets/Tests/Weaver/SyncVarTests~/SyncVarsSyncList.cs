using Mirror;

namespace SyncVarTests.SyncVarsSyncList
{

    class SyncVarsSyncList : NetworkBehaviour
    {
        [SyncVar]
        SyncList<int> syncints;
    }
}
