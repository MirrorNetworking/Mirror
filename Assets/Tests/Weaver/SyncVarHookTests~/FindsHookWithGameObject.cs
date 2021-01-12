using Mirror;
using UnityEngine;

namespace SyncVarHookTests.FindsHookWithGameObject
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
