using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncVarsTooManyHookParams
{
    class SyncVarsTooManyHookParams : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        public void TakeDamage(int amount)
        {
            if (!isServer)
                return;

            health -= amount;
        }

        void OnChangeHealth(int oldHealth, int newHealth, int extraFunParameter)
        {
            // do things with your health bar
        }
    }
}
