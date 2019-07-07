# SyncHashSet

`SyncHashSet` are sets similar to C\# [HashSet\<T\>](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1) that synchronize their contents from the server to the clients.

A SyncHashSet can contain items of the following types:

-   Basic type (byte, int, float, string, UInt64, etc)

-   Built-in Unity math type (Vector3, Quaternion, etc)

-   NetworkIdentity

-   Game object with a NetworkIdentity component attached.

-   Structure with any of the above

## Usage

Create a class that derives from SyncHashSet for your specific type. This is necessary because Mirror will add methods to that class with the weaver. Then add a SyncHashSet field to your NetworkBehaviour class. For example:

```cs
class Player : NetworkBehaviour {

    class SyncSkillSet : SyncHashSet<string> {}

    SyncSkillSet skills = new SyncSkillSet();

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

You can also detect when a SyncHashSet changes. This is useful for refreshing your character in the client or determining when you need to update your database. Subscribe to the Callback event typically during `Start`, `OnClientStart` or `OnServerStart` for that. Note that by the time you subscribe, the set will already be initialized, so you will not get a call for the initial data, only updates.

```cs
class Player : NetworkBehaviour
{
    class SyncSetBuffs : SyncHashSet<string> {};

    SyncSetBuffs buffs = new SyncSetBuffs();

    // this will add the delegate on the client.
    // Use OnStartServer instead if you want it on the server
    public override void OnStartClient()
    {
        buffs.Callback += OnBuffsChanged;
    }

    void OnBuffsChanged(SyncSetBuffs.Operation op, string buff)
    {
        switch (op) 
        {
            case SyncSetBuffs.Operation.OP_ADD:
                // we added a buff, draw an icon on the character
                break;
            case SyncSetBuffs.Operation.OP_CLEAR:
                // clear all buffs from the character
                break;
            case SyncSetBuffs.Operation.OP_REMOVE:
                // We removed a buff from the character
                break;
        }
    }
}
```
