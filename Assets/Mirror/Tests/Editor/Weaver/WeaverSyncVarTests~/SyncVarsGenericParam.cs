using Mirror;

namespace WeaverSyncVarTests.SyncVarsGenericParam
{
    class SyncVarsGenericParam : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        class MySyncVar<T>
        {
            T abc;
        }
        [SyncVar]
        MySyncVar<int> invalidVar = new MySyncVar<int>();

        public void TakeDamage(int amount)
        {
            if (!IsServer)
                return;

            health -= amount;
        }

        void OnChangeHealth(int oldHealth, int newHealth)
        {
            // do things with your health bar
        }
    }
}
