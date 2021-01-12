using Mirror;

namespace SyncVarTests.SyncVarGenericFields
{
    class SyncVarGenericFields<T> : NetworkBehaviour
    {
        [SyncVar]
        T invalidVar;
    }
}
