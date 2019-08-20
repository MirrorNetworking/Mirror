# SyncVar Hook

The hook attribute can be used to specify a function to be called when the SyncVar changes value on the client.  This ensures that all clients receive the proper variables from other clients.

-   The Hook method must have a single parameter of the same type as the SyncVar property.  This parameter should have a unique name, e.g. newValue.

-   Do not try to set the property value from inside the hook.  The property value will be updated after the hook completes.

-   Reference the hook parameter inside the hook to use the new value.  Referencing the property value will be the old value, in case you need to compare.

Below is a simple example of assigning a random color to each player when they're spawned on the server.  All clients will see all players in the correct colors, even if they join later.

```cs
using UnityEngine;
using UnityEngine.Networking;

public class PlayerController : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
    }

    [SyncVar(hook = nameof(SetColor))]
    Color playerColor = Color.black;

    // Unity makes a clone of the Material every time GetComponent<Renderer>().material is used.
    // Cache it here and Destroy it in OnDestroy to prevent a memory leak.
    Material cachedMaterial;

    void SetColor(Color color)
    {
        if (cachedMaterial == null)
            cachedMaterial = GetComponent<Renderer>().material;

        cachedMaterial.color = color;
    }

    void OnDestroy()
    {
        Destroy(cachedMaterial);
    }
}
```
