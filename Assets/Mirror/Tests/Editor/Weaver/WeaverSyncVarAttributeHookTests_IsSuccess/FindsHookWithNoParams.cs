using Mirror;
using UnityEngine;

namespace WeaverSyncVarHookTests.FindsHookWithOtherOverloadsInOrder
{
    class FindsHookWithNoParams : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth()
        {

        }
    }
}
