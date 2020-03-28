# SyncVars

[![SyncVar video tutorial](../../images/video_tutorial.png)](https://www.youtube.com/watch?v=T7AoozedYfI&list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP&index=5)

SyncVars are properties of classes that inherit from NetworkBehaviour, which are synchronized from the server to clients. When a game object is spawned, or a new player joins a game in progress, they are sent the latest state of all SyncVars on networked objects that are visible to them. Use the `SyncVar` custom attribute to specify which variables in your script you want to synchronize.

The state of SyncVars is applied to game objects on clients before `OnStartClient()` is called, so the state of the object is always up-to-date inside `OnStartClient()`.

SyncVars can use any [type supported by Mirror](../DataTypes.md). You can have up to 64 SyncVars on a single NetworkBehaviour script, including SyncLists (see next section, below).

The server automatically sends SyncVar updates when the value of a SyncVar changes, so you do not need to track when they change or send information about the changes yourself. Changing a value in the inspector will not trigger an update.

>   The [SyncVar hook](SyncVarHook.md) attribute can be used to specify a method to be called when the SyncVar changes value on the client.

## SyncVar Example
Let's say we have a networked object with a script called Enemy:

``` cs
public class Enemy : NetworkBehaviour
{
    [SyncVar]
    public int health = 100;

    void OnMouseUp()
    {
        NetworkIdentity ni = NetworkClient.connection.identity;
        PlayerController pc = ni.GetComponent<PlayerController>();
        pc.currentTarget = gameObject;
    }
}
```

The `PlayerController` might look like this:

``` cs
public class PlayerController : NetworkBehaviour
{
    public GameObject currentTarget;

    void Update()
    {
        if (isLocalPlayer)
            if (currentTarget != null)
                if (currentTarget.tag == "Enemy")
                    if (Input.GetKeyDown(KeyCode.X))
                        CmdShoot(currentTarget);
    }

    [Command]
    public void CmdShoot(GameObject enemy)
    {
        // Do your own shot validation here because this runs on the server
        enemy.GetComponent<Enemy>().health -= 5;
    }
}
```

In this example, when a Player clicks on an Enemy, the networked enemy game object is assigned to `PlayerController.currentTarget`. When the player presses X, with a correct target selected, that target is passed through a Command, which runs on the server, to decrement the `health` SyncVar. All clients will be updated with that new value. You can then have a UI on the enemy to show the current value.

## Class inheritance

SyncVars work with class inheritance. Consider this example:

```cs
class Pet : NetworkBehaviour
{
    [SyncVar] 
    String name;
}

class Cat : Pet
{
    [SyncVar]
    public Color32 color;
}
```

You can attach the Cat component to your cat prefab, and it will synchronize both it's `name` and `color`.

>   **Warning** Both `Cat` and `Pet` should be in the same assembly. If they are in separate assemblies, make sure not to change `name` from inside `Cat` directly, add a method to `Pet` instead. 
