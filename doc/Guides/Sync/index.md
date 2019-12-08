# State Synchronization

State synchronization refers to the synchronization of values such as integers, floating point numbers, strings and boolean values belonging to scripts.

State synchronization is done from the Server to remote clients. The local client does not have data serialized to it. It does not need it, because it shares the Scene with the server. However, SyncVar hooks are called on local clients.

Data is not synchronized in the opposite direction - from remote clients to the server. To do this, you need to use Commands.
-   [SyncVars](SyncVars.md)  
    SyncVars are variables of scripts that inherit from NetworkBehaviour, which are synchronized from the server to clients. 
-   [SyncEvents](SyncEvent.md)  
    SyncEvents are networked events like ClientRpc’s, but instead of calling a function on the game object, they trigger Events instead.
-   [SyncLists](SyncLists.md)  
    SyncLists contain lists of values and synchronize data from servers to clients.
-   [SyncDictionary](SyncDictionary.md)  
    A SyncDictionary is an associative array containing an unordered list of key, value pairs.
-   [SyncHashSet](SyncHashSet.md)  
    An unordered set of values that do not repeat.
-   [SyncSortedSet](SyncSortedSet.md)  
    A sorted set of values that do not repeat.

## Sync To Owner

It is often the case when you don't want some player data visible to other players. In the inspector change the "Network Sync Mode" from "Observers" (default) to "Owner" to let Mirror know to synchronize the data only with the owning client.

For example, suppose you are making an inventory system. Suppose player A,B and C are in the same area. There will be a total of 12 objects in the entire network:

* Client A has Player A (himself),  Player B and Player C
* Client B has Player A ,  Player B (himself) and Player C
* Client C has Player A ,  Player B and Player C (himself)
* Server has Player A, Player B, Player C

each one of them would have an Inventory component

Suppose Player A picks up some loot.  The server adds the loot to Player's A inventory,  which would have a [SyncLists](SyncLists.md) of Items. 

By default,  Mirror now has to synchronize player A's inventory everywhere,  that means sending an update message to client A,  client B and client C,  because they all have a copy of Player A. This is wasteful,  Client B and Client C do not need to know about Player's A inventory,  they never see it on screen.  It is also a security problem,  someone could hack the client and display other people's inventory and use it to their advantage.

If you set the "Network Sync Mode"  in the Inventory component to "Owner",  then Player A's inventory will only be synchronized with Client A.  

Now,  suppose instead of 3 people you have 50 people in an area and one of them picks up loot.  It means that instead of sending 50 messages to 50 different clients,  you would only send 1.  This can have a big impact in bandwith in your game.

Other typical use cases include quests,  player's hand in a card game, skills, experience, or any other data you don't need to share with other players.

## Advanced State Synchronization

In most cases, the use of SyncVars is enough for your game scripts to serialize their state to clients. However in some cases you might require more complex serialization code. This page is only relevant for advanced developers who need customized synchronization solutions that go beyond Mirror’s normal SyncVar feature.

## Custom Serialization Functions

To perform your own custom serialization, you can implement virtual functions on NetworkBehaviour to be used for SyncVar serialization. These functions are:

```cs
public virtual bool OnSerialize(NetworkWriter writer, bool initialState);
```

```cs
public virtual void OnDeserialize(NetworkReader reader, bool initialState);
```

Use the `initialState` flag to differentiate between the first time a game object is serialized and when incremental updates can be sent. The first time a game object is sent to a client, it must include a full state snapshot, but subsequent updates can save on bandwidth by including only incremental changes. Note that SyncVar hook functions are not called when `initialState` is true; they are only called for incremental updates.

If a class has SyncVars, then implementations of these functions are added automatically to the class, meaning that a class that has SyncVars cannot also have custom serialization functions.

The `OnSerialize` function should return true to indicate that an update should be sent. If it returns true, the dirty bits for that script are set to zero. If it returns false, the dirty bits are not changed. This allows multiple changes to a script to be accumulated over time and sent when the system is ready, instead of every frame.

Although this works,  it is usually better to let Mirror generate these methods and provide [custom serializers](../DataTypes.md) for your specific field.

## Serialization Flow

Game objects with the Network Identity component attached can have multiple scripts derived from `NetworkBehaviour`. The flow for serializing these game objects is:

On the server:
-   Each `NetworkBehaviour` has a dirty mask. This mask is available inside `OnSerialize` as `syncVarDirtyBits`
-   Each SyncVar in a `NetworkBehaviour` script is assigned a bit in the dirty mask.
-   Changing the value of SyncVars causes the bit for that SyncVar to be set in the dirty mask
-   Alternatively, calling `SetDirtyBit` writes directly to the dirty mask
-   NetworkIdentity game objects are checked on the server as part of it’s update loop
-   If any `NetworkBehaviours` on a `NetworkIdentity` are dirty, then an `UpdateVars` packet is created for that game object
-   The `UpdateVars` packet is populated by calling `OnSerialize` on each `NetworkBehaviour` on the game object
-   `NetworkBehaviours` that are not dirty write a zero to the packet for their dirty bits
-   `NetworkBehaviours` that are dirty write their dirty mask, then the values for the SyncVars that have changed
-   If `OnSerialize` returns true for a `NetworkBehaviour`, the dirty mask is reset for that `NetworkBehaviour` so it does not send again until its value changes.
-   The `UpdateVars` packet is sent to ready clients that are observing the game object

On the client:
-   an `UpdateVars packet` is received for a game object
-   The `OnDeserialize` function is called for each `NetworkBehaviour` script on the game object
-   Each `NetworkBehaviour` script on the game object reads a dirty mask.
-   If the dirty mask for a `NetworkBehaviour` is zero, the `OnDeserialize` function returns without reading any more
-   If the dirty mask is non-zero value, then the `OnDeserialize` function reads the values for the SyncVars that correspond to the dirty bits that are set
-   If there are SyncVar hook functions, those are invoked with the value read from the stream.

So for this script:

```cs
public class data : NetworkBehaviour
{
    [SyncVar]
    public int int1 = 66;

    [SyncVar]
    public int int2 = 23487;

    [SyncVar]
    public string MyString = "Example string";
}
```

The following code sample demonstrates the generated `OnSerialize` function:

```cs
public override bool OnSerialize(NetworkWriter writer, bool forceAll)
{
    if (forceAll)
    {
        // The first time a game object is sent to a client, send all the data (and no dirty bits)
        writer.WritePackedUInt32((uint)this.int1);
        writer.WritePackedUInt32((uint)this.int2);
        writer.Write(this.MyString);
        return true;
    }

    bool wroteSyncVar = false;
    if ((base.get_syncVarDirtyBits() & 1u) != 0u)
    {
        if (!wroteSyncVar)
        {
            // Write dirty bits if this is the first SyncVar written
            writer.WritePackedUInt32(base.get_syncVarDirtyBits());
            wroteSyncVar = true;
        }
        writer.WritePackedUInt32((uint)this.int1);
    }

    if ((base.get_syncVarDirtyBits() & 2u) != 0u)
    {
        if (!wroteSyncVar)
        {
            // Write dirty bits if this is the first SyncVar written
            writer.WritePackedUInt32(base.get_syncVarDirtyBits());
            wroteSyncVar = true;
        }
        writer.WritePackedUInt32((uint)this.int2);
    }

    if ((base.get_syncVarDirtyBits() & 4u) != 0u)
    {
        if (!wroteSyncVar)
        {
            // Write dirty bits if this is the first SyncVar written
            writer.WritePackedUInt32(base.get_syncVarDirtyBits());
            wroteSyncVar = true;
        }
        writer.Write(this.MyString);
    }

    if (!wroteSyncVar)
    {
        // Write zero dirty bits if no SyncVars were written
        writer.WritePackedUInt32(0);
    }
    return wroteSyncVar;
}
```

The following code sample demonstrates the `OnDeserialize` function:

```cs
public override void OnDeserialize(NetworkReader reader, bool initialState)
{
    if (initialState)
    {
        this.int1 = (int)reader.ReadPackedUInt32();
        this.int2 = (int)reader.ReadPackedUInt32();
        this.MyString = reader.ReadString();
        return;
    }

    int num = (int)reader.ReadPackedUInt32();
    if ((num & 1) != 0)
    {
        this.int1 = (int)reader.ReadPackedUInt32();
    }

    if ((num & 2) != 0)
    {
        this.int2 = (int)reader.ReadPackedUInt32();
    }

    if ((num & 4) != 0)
    {
        this.MyString = reader.ReadString();
    }
}
```

If a `NetworkBehaviour` has a base class that also has serialization functions, the base class functions should also be called.

Note that the `UpdateVar` packets created for game object state updates may be aggregated in buffers before being sent to the client, so a single transport layer packet may contain updates for multiple game objects.
