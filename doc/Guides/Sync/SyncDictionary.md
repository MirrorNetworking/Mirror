# SyncDictionary

A SyncDictionary is an associative array containing an unordered list of key, value pairs. Keys and values can be of the following types:
-   Basic type (byte, int, float, string, UInt64, etc)
-   Built-in Unity math type (Vector3, Quaternion, etc)
-   NetworkIdentity
-   Game object with a NetworkIdentity component attached.
-   Struct with any of the above

SyncDictionary works much like [SyncLists](SyncLists.md): when you make a change on the server the change is propagated to all clients and the Callback is called.


To use it, create a class that derives from SyncDictionary for your specific type. This is necessary because the Weaver will add methods to that class. Then add a field to your NetworkBehaviour class.

> Note that by the time you subscribe to the callback, the dictionary will already be initialized, so you will not get a call for the initial data, only updates.</p>

>Note SyncDictionaries must be initialized in the constructor, not in Startxxx().  You can make them readonly to ensure correct usage.

## Simple Example

```cs
using UnityEngine;
using Mirror;

public class ExamplePlayer : NetworkBehaviour
{
    public class SyncDictionaryStringItem : SyncDictionary<string, Item> {}

    public struct Item
    {
        public string name;
        public int hitPoints;
        public int durability;
    }

    public readonly SyncDictionaryStringItem Equipment = new SyncDictionaryStringItem();

    public override void OnStartServer()
    {
        Equipment.Add("head", new Item { name = "Helmet", hitPoints = 10, durability = 20 });
        Equipment.Add("body", new Item { name = "Epic Armor", hitPoints = 50, durability = 50 });
        Equipment.Add("feet", new Item { name = "Sneakers", hitPoints = 3, durability = 40 });
        Equipment.Add("hands", new Item { name = "Sword", hitPoints = 30, durability = 15 });
    }

    public override void OnStartClient()
    {
        // Equipment is already populated with anything the server set up
        // but we can subscribe to the callback in case it is updated later on
        Equipment.Callback += OnEquipmentChange;
    }

    void OnEquipmentChange(SyncDictionaryStringItem.Operation op, string key, Item item)
    {
        // equipment changed,  perhaps update the gameobject
        Debug.Log(op + " - " + key);
    }
}
```

By default, SyncDictionary uses a Dictionary to store it's data. If you want to use a different `IDictionary `implementation such as `SortedList` or `SortedDictionary`, add a constructor to your SyncDictionary implementation and pass a dictionary to the base class. For example:

```cs
public class ExamplePlayer : NetworkBehaviour
{
    public class SyncDictionaryStringItem : SyncDictionary<string, Item> 
    {
        public SyncDictionaryStringItem() : base (new SortedList<string,Item>()) {}
    }
    
    public readonly SyncDictionaryStringItem Equipment = new SyncDictionaryStringItem();
}
```

