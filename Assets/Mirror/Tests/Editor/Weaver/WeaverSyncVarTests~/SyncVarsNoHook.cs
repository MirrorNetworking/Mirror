using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncVarsNoHook : NetworkBehaviour
    {
        [SyncVar(hook = "OnChangeHealth")]
        int health;

        public void TakeDamage(int amount)
        {
            if (!IsServer)
                return;

            health -= amount;
        }
    }
}
