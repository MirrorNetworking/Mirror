using Mirror;

namespace WeaverSyncVarTests.SyncVarsStatic
{
    class SyncVarsStatic : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        [SyncVar]
        static int invalidVar = 123;

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
