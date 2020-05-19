using Mirror;
using UnityEngine;

namespace WeaverSyncVarHookTests.AutoDetectsHookWithGameObject
{
    class AutoDetectsHookWithGameObject : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onTargetChanged))]
        GameObject target;

        void onTargetChanged(GameObject oldValue, GameObject newValue)
        {

        }
    }
}
