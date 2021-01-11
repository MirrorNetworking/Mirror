using Mirror;
using UnityEngine;

namespace WeaverSyncVarTests.SyncVarsDifferentModule
{
    class SyncVarsDifferentModule : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        [SyncVar]
        TextMesh invalidVar;

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
