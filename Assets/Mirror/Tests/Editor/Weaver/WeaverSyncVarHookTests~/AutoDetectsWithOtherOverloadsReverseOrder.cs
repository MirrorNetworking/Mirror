using Mirror;
namespace WeaverSyncVarHookTests.AutoDetectsWithOtherOverloadsReverseOrder
{
    class AutoDetectsWithOtherOverloadsReverseOrder : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue, float someOtherValue)
        {

        }
        void onChangeHealth(int newValue)
        {

        }
    }
}
