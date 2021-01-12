using Mirror;
using UnityEngine;

namespace SyncVarHookTests.FindsHookWithOtherOverloadsInReverseOrder
{
    class FindsHookWithOtherOverloadsInReverseOrder : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(Vector3 anotherValue, bool secondArg)
        {

        }

        void onChangeHealth(int oldValue, int newValue)
        {

        }
    }
}
