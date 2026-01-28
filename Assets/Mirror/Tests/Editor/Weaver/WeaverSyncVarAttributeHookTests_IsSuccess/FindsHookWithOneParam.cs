using Mirror;
using UnityEngine;

namespace WeaverSyncVarHookTests.FindsHookWithOtherOverloadsInOrder
{
    class FindsHookWithOneParam : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int oldValue)
        {

        }
    }
}
