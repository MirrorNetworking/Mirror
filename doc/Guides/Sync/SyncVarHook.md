# SyncVar Hook

[![SyncVar hook video tutorial](../../images/video_tutorial.png)](https://www.youtube.com/watch?v=T7AoozedYfI&list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP&index=5)

The hook attribute can be used to specify a function to be called when the SyncVar changes value on the client.
-   The Hook method should have one of the following method signature:
```csharp
// T is the SyncVar field type
void onValueChange(T newValue)
void onValueChange(T oldValue, T newValue)
void onValueChange(T oldValue, T newValue, bool initialState)
```
-   The type of the `newValue` and `oldValue` must be the same type as the SyncVar field
-   If the `initialState` parameter is used it must be a `bool`
-   The Hook is always called after the property value is set. You don't need to set it yourself.
-   The Hook only fires for changed values, and changing a value in the inspector will not trigger an update.
    -   hooks using `initialState` will also fire when the created **even if the value is unchanged**.
-   If using multiple functions with the same name the `hookParameter` option in the `SyncVar` attribute can be used to pick which one to use.
    -   For example: 
```csharp
    [SyncVar(hook = nameof(SetColor), hookParameter = HookParameter.OldNewInitial)]
    Color playerColor;
```
-   As of version 11.1.4 (March 2020) and later, hooks can be virtual methods and overriden in a derived class.
-   As of version 13.?.? (May 2020) and later, there are multiple method signature which can be used for hooks.

Below is a simple example of assigning a random color to each player when they're spawned on the server.  All clients will see all players in the correct colors, even if they join later.

```cs
using UnityEngine;
using Mirror;

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

    void SetColor(Color newColor)
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

The `void onValueChange(T oldValue, T newValue, bool initialState)` method signature can be used to do extra work the first time the object is spawned.

*note syncVars are set before OnStartClient*

```cs
using Mirror;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SyncVar(hook = nameof(SetColor), hookParameter = HookParameter.OldNewInitial)]
    Color playerColor = Color.black;

    [SerializeField] GameObject prefab;

    // Unity makes a clone of the Material every time GetComponent<Renderer>().material is used.
    // Cache it here and Destroy it in OnDestroy to prevent a memory leak.
    Material cachedMaterial;
    GameObject target;

    public override void OnStartServer()
    {
        base.OnStartServer();
        playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
    }

    void SetColor(Color oldColor, Color newColor, bool initialState)
    {
        // because of initialState, this hook will be called when spawning even if the value is unchanged
        if (initialState)
        {
            // spawn target prefab when PlayerController is first spawned 
            target = Instantiate(prefab);
            cachedMaterial = target.GetComponent<Renderer>().material;
        }

        cachedMaterial.color = newColor;
    }

    void OnDestroy()
    {
        Destroy(target);
        Destroy(cachedMaterial);
    }
}
```