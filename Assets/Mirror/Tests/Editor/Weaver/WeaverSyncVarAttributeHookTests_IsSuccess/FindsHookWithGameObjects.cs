using Mirror;
using UnityEngine;

namespace WeaverSyncVarHookTests.FindsHookWithGameObjects
{
    class FindsHookWithGameObjects : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onTargetChanged))]
        GameObject target;

        void onTargetChanged(GameObject oldValue, GameObject newValue)
        {

        }
    }
}
