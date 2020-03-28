using UnityEngine;

namespace Mirror.Examples
{
    public class Health : NetworkBehaviour
    {
        [SyncVar] public int health = 10;
    }
}
