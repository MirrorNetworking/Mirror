using Mirror;
using GodotEngine;

namespace WeaverSyncVarHookTests.FindsHookWithOtherOverloadsInReverseOrder
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
