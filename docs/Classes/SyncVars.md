# SyncVars

SyncVars are variables of scripts that inherit from NetworkBehaviour, which are synchronized from the server to clients. When a GameObject is spawned, or a new player joins a game in progress, they are sent the latest state of all SyncVars on networked objects that are visible to them. Use the `SyncVar` custom attribute to specify which variables in your script you want to synchronize, like this:

```cs
class Player : NetworkBehaviour
{
    [SyncVar]
    int health;

    public void TakeDamage(int amount)
    {
        if (!isServer)
            return;

        health -= amount;
    }
}
```

The state of SyncVars is applied to GameObjects on clients before OnStartClient() is called, so the state of the object is always up-to-date inside OnStartClient().

SyncVars can be basic types such as integers, strings and floats. They can also be Unity types such as Vector3 and user-defined structs, but updates for struct SyncVars are sent as monolithic updates, not incremental changes if fields within a struct change. You can have up to 32 SyncVars on a single NetworkBehaviour script, including SyncLists (see next section, below).

The server automatically sends SyncVar updates when the value of a SyncVar changes, so you do not need to track when they change or send information about the changes yourself.

## SyncVar Hooks

The [SyncVar hook](SyncVarHook) attribute can be used to specify a method to be called when the SyncVar changes value on the client.
