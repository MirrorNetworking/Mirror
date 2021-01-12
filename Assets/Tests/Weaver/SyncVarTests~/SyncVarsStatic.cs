using Mirror;

namespace SyncVarTests.SyncVarsStatic
{
    class SyncVarsStatic : NetworkBehaviour
    {
        [SyncVar]
        static int invalidVar = 123;
    }
}
