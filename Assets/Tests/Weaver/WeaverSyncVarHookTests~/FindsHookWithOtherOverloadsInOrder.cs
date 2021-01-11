using Mirror;
using UnityEngine;

namespace WeaverSyncVarHookTests.FindsHookWithOtherOverloadsInOrder
{
    class FindsHookWithOtherOverloadsInOrder : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int oldValue, int newValue)
        {

        }

        void onChangeHealth(Vector3 anotherValue, bool secondArg)
        {

        }
    }
}
