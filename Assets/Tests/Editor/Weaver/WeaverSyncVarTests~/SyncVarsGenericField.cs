using Mirror;

namespace WeaverSyncVarTests.SyncVarGenericFields
{
    class SyncVarGenericFields<T> : NetworkBehaviour
    {
        [SyncVar]
        T invalidVar;
    }
}
