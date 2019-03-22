# SyncDictionary

SyncDictionaries contain an unordered list of items, each with two fields: a "key" and a "value". Using the "key", like an int or string identifier, you can retrieve the "value", which could be a name in string form, a struct of player data, or some JSON about a match. SyncDictionaries work much like SyncLists--when you make a change on the server, the change is propagated to all clients and the Callback is called.

## Simple Example

```cs
using UnityEngine;
using Mirror;

public class ExamplePlayer : NetworkBehaviour
{
    public class SyncDictionaryIntPlayer : SyncDictionary<int, PlayerData> { }

    public struct PlayerData
    {
        public int Health;
        public float Speed;
        public Vector3 Position;
    }

    public SyncDictionaryIntPlayer Players;

    private void Start()
    {
        if (isLocalPlayer)
        {
            Players.Callback += PlayersChanged;
            CmdRegisterPlayer();
        }
    }

    [Command]
    public void CmdRegisterPlayer()
    {
        Players.Add(connectionToClient.connectionId, new PlayerData { Health = 100, Speed = 5f, Position = Vector3.zero });
    }

    private void PlayersChanged(SyncDictionaryIntPlayer.Operation op, int key, PlayerData item)
    {
        Debug.Log(op + " - " + key);
    }
}
```
