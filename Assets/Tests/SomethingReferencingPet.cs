using UnityEngine;
using Mirror;

public class SomethingReferencingPet : NetworkBehaviour
{
    public GameObject petNonSync;
    [SyncVar] public GameObject petSync;

    void Update()
    {
        Debug.Log("petNonSync=" + petNonSync + " petSync=" + petSync);
    }
}
