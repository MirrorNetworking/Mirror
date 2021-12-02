using Mirror;

namespace WeaverSyncVarTests.SyncVarsGenericParam
{
    class SyncVarsGenericParam : NetworkBehaviour
    {
        struct MySyncVar<T>
        {
            T abc;
        }

        [SyncVar]
        MySyncVar<int> invalidVar = new MySyncVar<int>();
    }
}
