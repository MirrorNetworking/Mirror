# SyncVar Hook

[![SyncVar hook video tutorial](../../images/video_tutorial.png)](https://www.youtube.com/watch?v=T7AoozedYfI&list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP&index=5)

The hook attribute can be used to specify a function to be called when the SyncVar changes value on the client.  This ensures that all clients receive the proper variables from other clients.
-   The Hook method must have a two parameters of the same type as the SyncVar property. One for the old value, one for the new value.
-   The Hook is always called after the value was set. You don't need to set it yourself.

Below is a simple example of assigning a random color to each player when they're spawned on the server.  All clients will see all players in the correct colors, even if they join later.

>   Note:  The signature for hook methods was changed in version 7.0 (Feb 2020) to having 2 parameters (old and new values).  If you're on an older version, hook methods just have one parameter (new value).

```cs
using UnityEngine;
using UnityEngine.Networking;

public class PlayerController : NetworkBehaviour
{

    [SyncVar(hook = nameof(SetColor))]
    Color playerColor = Color.black;

    // Unity makes a clone of the Material every time GetComponent<Renderer>().material is used.
    // Cache it here and Destroy it in OnDestroy to prevent a memory leak.
    Material cachedMaterial;

    public override void OnStartServer()
    {
        base.OnStartServer();
        playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
    }

    void SetColor(Color oldColor, Color newColor)
    {
        if (cachedMaterial == null)
            cachedMaterial = GetComponent<Renderer>().material;

        cachedMaterial.color = newColor;
    }

    void OnDestroy()
    {
        Destroy(cachedMaterial);
    }
}
```
