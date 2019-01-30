# State synchronization

State synchronization refers to the synchronization of values such as integers, floating point numbers, strings and boolean values belonging to scripts.

State synchronization is done from the Server to remote clients. The local client does not have data serialized to it. It does not need it, because it shares the Scene with the server. However, SyncVar hooks are called on local clients.

Data is not synchronized in the opposite direction - from remote clients to the server. To do this, you need to use Commands.

## SyncVars

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

## SyncLists

While SyncVars contain values, SyncLists contain lists of values. SyncList contents are included in initial state updates along with SyncVar states. Since SyncList is a class which synchronises its own contents, SyncLists do not require the SyncVar attribute. The following types of SyncList are available for basic types:

-   SyncListString
-   SyncListFloat
-   SyncListInt
-   SyncListUInt
-   SyncListBool

There is also SyncListSTRUCT, which you can use to synchronize lists of your own struct types. When using SyncListSTRUCT, the struct type that you choose to use can contain members of basic types, arrays, and common Unity types. They cannot contain complex classes or generic containers, and only public variables in these structs are serialized.

SyncLists have a SyncListChanged delegate named Callback that allows clients to be notified when the contents of the list change. This delegate is called with the type of operation that occurred, and the index of the item that the operation was for.

```cs
public class MyScript : NetworkBehaviour
{
    public struct Buf
    {
        public int id;
        public string name;
        public float timer;
    };
            
    public class TestBufs : SyncListSTRUCT<Buf> {}

    TestBufs m_bufs = new TestBufs();
    
    void BufChanged(Operation op, int itemIndex)
    {
        Debug.Log("buf changed:" + op);
    }
    
    void Start()
    {
        m_bufs.Callback = BufChanged;
    }
}
```
