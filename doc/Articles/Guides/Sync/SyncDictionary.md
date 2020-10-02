# SyncDictionary

A SyncDictionary is an associative array containing an unordered list of key, value pairs. Keys and values can be any [supported mirror type](../DataTypes.md). By default we use .Net [Dictionary](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=netcore-3.1) which may impose additional constraints on the keys and values.

SyncDictionary works much like [SyncLists](SyncLists.md): when you make a change on the server the change is propagated to all clients and the Callback is called. Only deltas are transmitted.

## Usage

Add a field to your NetworkBehaviour class fo type `SyncDictionary<Key, Value>`. For example:

> Note that by the time you subscribe to the callback, the dictionary will already be initialized, so you will not get a call for the initial data, only updates.</p>

>Note SyncDictionaries must be initialized in the constructor, not in Startxxx().  You can make them readonly to ensure correct usage.

## Simple Example

```cs
using UnityEngine;
using Mirror;

public struct Item
{
    public string name;
    public int hitPoints;
    public int durability;
}

public class ExamplePlayer : NetworkBehaviour
{
    public readonly SyncDictionary<string, Item> Equipment = new SyncDictionary<string, Item>();

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

    void OnEquipmentChange(SyncDictionary<string, Item>.Operation op, string key, Item item)
    {
        // equipment changed,  perhaps update the gameobject
        Debug.Log(op + " - " + key);
    }
}
```

By default, SyncDictionary uses a [Dictionary](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=netcore-3.1) to store it's data. If you want to use a different `IDictionary` implementation such as [SortedList](https://docs.microsoft.com/en-us/dotnet/api/system.collections.sortedlist?view=netcore-3.1) or [SortedDictionary](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2?view=netcore-3.1), then use `SyncIDictionary<Key,Value>` and pass the dictionary instance you want it to use.  For example:

```cs
public class ExamplePlayer : NetworkBehaviour
{
    public readonly SyncIDictionary<string, Item> Equipment = 
        new SyncIDictionary<string, Item>(new SortedList<string, Item>());
}

```

