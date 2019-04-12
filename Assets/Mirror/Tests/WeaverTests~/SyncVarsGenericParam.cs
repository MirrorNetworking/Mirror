using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
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
            if (!isServer)
                return;

            health -= amount;
        }

        void OnChangeHealth(int health)
        {
            // do things with your health bar
        }
    }
}
