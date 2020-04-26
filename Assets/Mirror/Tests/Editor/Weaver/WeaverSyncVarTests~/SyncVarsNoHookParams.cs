using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncVarsNoHookParams
{
    class SyncVarsNoHookParams : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        public void TakeDamage(int amount)
        {
            if (!isServer)
                return;

            health -= amount;
        }

        void OnChangeHealth()
        {
            // do things with your health bar
        }
    }
}
