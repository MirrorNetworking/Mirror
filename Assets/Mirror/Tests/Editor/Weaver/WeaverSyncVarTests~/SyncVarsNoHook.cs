using UnityEngine;
using Mirror;

namespace WeaverSyncVarTests.SyncVarsNoHook
{
    class SyncVarsNoHook : NetworkBehaviour
    {
        [SyncVar(hook = "OnChangeHealth")]
        int health;

        public void TakeDamage(int amount)
        {
            if (!isServer)
                return;

            health -= amount;
        }
    }
}
