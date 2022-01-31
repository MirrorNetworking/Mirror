using Mirror;
using UnityEngine;

namespace WeaverSyncVarTests.SyncVarsUnityComponent
{
    class SyncVarsUnityComponent : NetworkBehaviour
    {
        [SyncVar]
        TextMesh invalidVar;
    }
}
