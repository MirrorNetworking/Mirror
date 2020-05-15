using Mirror;
namespace WeaverSyncVarHookTests.AutoDetectsWithOtherOverloads
{
    class AutoDetectsWithOtherOverloads : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }

        void onChangeHealth(int newValue, float someOtherValue)
        {

        }
    }
}
