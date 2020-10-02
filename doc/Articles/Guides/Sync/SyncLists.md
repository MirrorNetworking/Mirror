# SyncLists

SyncLists are array based lists similar to C\# [List\<T\>](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=netframework-4.7.2) that synchronize their contents from the server to the clients.

A SyncList can contain any [supported mirror type](../DataTypes.md).

## Differences with HLAPI

HLAPI also supports SyncLists, but we have redesigned them to make them more efficient and easier to use. Some of the key differences include:
-   In HLAPI, SyncLists were synchronized immediately when they changed. If you add 10 elements, that means 10 separate messages. Mirror synchronizes SyncLists with the SyncVars. The 10 elements and other SyncVars are batched together into a single message. Mirror also respects the sync interval when synchronizing lists.
-   In HLAPI if you want a list of structs, you have to use `SyncListStruct<MyStructure>`, we changed it to just `SyncList<MyStructure>`
-   In HLAPI the Callback is a delegate. In Mirror we changed it to an event, so that you can add many subscribers.
-   In HLAPI the Callback tells you the operation and index. In Mirror, the callback also receives an item. We made this change so that we could tell what item was removed.
-   In HLAPI you must create a class that inherits from SyncList<T>. In Mirror you can just use SyncList<T> directly (starting with version 20.0.0)

## Usage

Add a SyncList field to your NetworkBehaviour class. For example:

```cs
public struct Item
{
    public string name;
    public int amount;
    public Color32 color;
}

public class Player : NetworkBehaviour
{
    readonly SyncList<Item> inventory = new SyncList<Item>();

    public int coins = 100;

    [Command]
    public void CmdPurchase(string itemName)
    {
        if (coins > 10)
        {
            coins -= 10;
            Item item = new Item
            {
                name = "Sword",
                amount = 3,
                color = new Color32(125, 125, 125, 255)
            };

            // during next synchronization,  all clients will see the item
            inventory.Add(item);
        }
    }
}
```

You can also detect when a SyncList changes in the client or server. This is useful for refreshing your character when you add equipment or determining when you need to update your database. Subscribe to the Callback event typically during `Start`, `OnClientStart`, or `OnServerStart` for that. 


> Note that by the time you subscribe, the list will already be initialized, so you will not get a call for the initial data, only updates.</p>
> Note SyncLists must be initialized in the constructor, not in Startxxx().  You can make them readonly to ensure correct usage.

```cs
class Player : NetworkBehaviour {

    readonly SyncList<Item> inventory = new SyncList<Item>();

    // this will add the delegates on both server and client.
    // Use OnStartClient instead if you just want the client to act upon updates
    void Start()
    {
        inventory.Callback += OnInventoryUpdated;
    }

    void OnInventoryUpdated(SyncList<Item>.Operation op, int index, Item oldItem, Item newItem)
    {
        switch (op)
        {
            case SyncList<Item>.Operation.OP_ADD:
                // index is where it got added in the list
                // item is the new item
                break;
            case SyncList<Item>.Operation.OP_CLEAR:
                // list got cleared
                break;
            case SyncList<Item>.Operation.OP_INSERT:
                // index is where it got added in the list
                // item is the new item
                break;
            case SyncList<Item>.Operation.OP_REMOVEAT:
                // index is where it got removed in the list
                // item is the item that was removed
                break;
            case SyncList<Item>.Operation.OP_SET:
                // index is the index of the item that was updated
                // item is the previous item
                break;
        }
    }
}
```

By default, SyncList uses a List to store it's data. If you want to use a different list implementation, pass it to the constructor of of SyncList. For example:

```cs
class Player : NetworkBehaviour {
    public SyncList<Item> inventory = new SyncList<Item>(new MyItemList());
}
```
