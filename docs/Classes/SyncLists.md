# SyncLists Overview

There are some very important optimizations when it comes to bandwidth done in Mirror.

## Channels

There was a bug in HLAPI that caused syncvar to be sent to every channel when they changed. If you had 10 channels, then all the variables would be sent 10 times during the same frame, all as different packages.

## SyncLists

HLAPI SyncLists sent a message for every change immediately. They did not respect the SyncInterval. If you add 10 items to a list, it means sending 10 messages.

In Mirror SyncList were redesigned. The lists queue up their changes, and the changes are sent as part of the syncvar synchronization. If you add 10 items, then only 1 message is sent with all changes according to the next SyncInterval.

In HLAPI,  if you wanted a list of structs,  you needed to use `SyncListStruct<MyStructure>`,  we changed it to just `SyncList<MyStructure>`

We also raised the limit from 32 SyncVars to 64 per NetworkBehavior.

A SyncList can only be of the following type

-   Basic type (byte, int, float, string, UInt64, etc)
-   Built-in Unity math type (Vector3, Quaternion, etc)
-   NetworkIdentity
-   GameObject with a NetworkIdentity component attached.
-   Structure with any of the above

## Usage

Create a class that derives from SyncList<T> for your specific type.  This is necesary because Mirror will add methods to that class with the weaver.  Then add a SyncList field to your NetworkBehaviour class.   For example:

```cs

public struct Item
{
    public string name;
    public int amount;
    public Color32 color;
}

class SyncListItem : SyncList<Item> {}

class Player : NetworkBehaviour {

    SyncListItem inventory;

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
                color = new Color32(125,125,125);
            };

            // during next synchronization,  all clients will see the item
            inventory.Add(item)
        }
    }

}
```

There are some ready made SyncLists you can use:
* `SyncListString`
* `SyncListFloat`
* `SyncListInt`
* `SyncListUInt`
* `SyncListBool`

You can also detect when a synclist changes in the client or server.  This is useful for refreshing your character when you add equipment or determining when you need to update your database.  Subscribe to the Callback event typically during `Start`,  `OnClientStart` or `OnServerStart` for that.   Note that by the time you subscribe,  the list will already be initialized,  so you will not get a call for the initial data, only updates.

```cs
class Player : NetworkBehaviour {

    SyncListItem inventory;

    // this will add the delegates on both server and client.
    // Use OnStartClient instead if you just want the client to act upon updates
    void Start()
    {
        myStringList.Callback += OnInventoryUpdated;
    }

    void OnInventoryUpdated(SyncListItem.Operation op, int index, Item item)
    {
        switch (op) 
        {
            case SyncListItem.Operation.OP_ADD:
                // index is where it got added in the list
                // item is the new item
                break;
            case SyncListItem.Operation.OP_CLEAR:
                // list got cleared
                break;
            case SyncListItem.Operation.OP_INSERT:
                // index is where it got added in the list
                // item is the new item
                break;
            case SyncListItem.Operation.OP_REMOVE:
                // index is where it got removed in the list
                // item is the item that was removed
                break;
            case SyncListItem.Operation.OP_REMOVEAT:
                // index is where it got removed in the list
                // item is the item that was removed
                break;
            case SyncListItem.Operation.OP_SET:
                // index is the index of the item that was updated
                // item is the previous item
                break;
            case SyncListItem.Operation.OP_SET:
                // index is the index of the item that was updated
                // item is the previous item
                break;
        }
    }
}
```
