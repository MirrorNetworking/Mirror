using UnityEngine;
using Mirror;

namespace WeaverSyncVarTests.SyncVarsWrongHookType
{
    class SyncVarsWrongHookType : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        public void TakeDamage(int amount)
        {
            if (!isServer)
                return;

            health -= amount;
        }

        void OnChangeHealth(bool oldHealth, bool newHealth)
        {
            // do things with your health bar
        }
    }
}
