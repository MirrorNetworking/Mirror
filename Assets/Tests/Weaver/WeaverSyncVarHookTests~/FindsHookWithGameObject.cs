using Mirror;
using UnityEngine;

namespace WeaverSyncVarHookTests.FindsHookWithGameObject
{
    class FindsHookWithGameObject : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onTargetChanged))]
        GameObject target;

        void onTargetChanged(GameObject oldValue, GameObject newValue)
        {

        }
    }
}
