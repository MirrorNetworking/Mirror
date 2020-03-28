using UnityEngine;

namespace Mirror.Examples
{
    public class Health : NetworkBehaviour
    {
        [SyncVar] public int health = 10;

        [ServerCallback]
        public void Update()
        {
            health = Random.Range(1, 10);
        }
    }
}
