using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
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
