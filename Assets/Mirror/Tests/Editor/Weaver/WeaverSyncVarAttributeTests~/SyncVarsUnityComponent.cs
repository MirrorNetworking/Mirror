using Mirror;
using GodotEngine;

namespace WeaverSyncVarTests.SyncVarsGodotComponent
{
    class SyncVarsGodotComponent : NetworkBehaviour
    {
        [SyncVar]
        TextMesh invalidVar;
    }
}
