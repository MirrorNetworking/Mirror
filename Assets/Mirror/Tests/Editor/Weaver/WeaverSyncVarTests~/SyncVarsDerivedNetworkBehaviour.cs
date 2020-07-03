using Mirror;

namespace WeaverSyncVarTests.SyncVarsDerivedNetworkBehaviour
{
    class SyncVarsDerivedNetworkBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        class MySyncVar : NetworkBehaviour
        {
            int abc = 123;
        }
        [SyncVar]
        MySyncVar invalidVar = new MySyncVar();

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
