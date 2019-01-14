using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour
{
    public GameObject petNonSync;
    [SyncVar] public GameObject petSync;

    void Update()
    {
        Debug.Log("Player.petNonSync=" + petNonSync + " petSync=" + petSync);
    }
}
