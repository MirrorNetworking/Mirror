# SyncSortedSet

`SyncSortedSet` are sets similar to C\# [SortedSet\<T\>](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.sortedset-1) that synchronize their contents from the server to the clients.

Unlike SyncHashSets, all elements in a SyncSortedSet are sorted when they are inserted. Please note this has some performance implications.

A SyncSortedSet can contain any [supported mirror type](../DataTypes.md) 

## Usage

Add a SyncSortedSet field to your NetworkBehaviour class. For example:

```cs
class Player : NetworkBehaviour {
    readonly SyncSortedSet<string> skills = new SyncSortedSet<string>();

    int skillPoints = 10;

    [Command]
    public void CmdLearnSkill(string skillName)
    {
        if (skillPoints > 1)
        {
            skillPoints--;

            skills.Add(skillName);
        }
    }
}
```

You can also detect when a SyncSortedSet changes. This is useful for refreshing your character in the client or determining when you need to update your database. Subscribe to the Callback event typically during `Start`, `OnClientStart` or `OnServerStart` for that. 

> Note that by the time you subscribe, the set will already be initialized, so you will not get a call for the initial data, only updates.</p>
> Note SyncSets must be initialized in the constructor, not in Startxxx().  You can make them readonly to ensure correct usage.

```cs
class Player : NetworkBehaviour
{
    readonly SyncSortedSet<string> buffs = new SyncSortedSet<string>();

    // this will add the delegate on the client.
    // Use OnStartServer instead if you want it on the server
    public override void OnStartClient()
    {
        buffs.Callback += OnBuffsChanged;
    }

    void OnBuffsChanged(SyncSortedSet<string>.Operation op, string buff)
    {
        switch (op) 
        {
            case SyncSortedSet<string>.Operation.OP_ADD:
                // we added a buff, draw an icon on the character
                break;
            case SyncSortedSet<string>.Operation.OP_CLEAR:
                // clear all buffs from the character
                break;
            case SyncSortedSet<string>.Operation.OP_REMOVE:
                // We removed a buff from the character
                break;
        }
    }
}
```
