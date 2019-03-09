# Custom Player Spawning

The Network Manager offers a built-in simple player spawning feature, however you may want to customize the player spawning process - for example to assign a color to each new player spawned.

To do this you need to override the default behavior of the Network Manager with your own script.

When the Network Manager adds a player, it also instantiates a GameObject from the Player Prefab and associates it with the connection. To do this, the Network Manager calls NetworkServer.AddPlayerForConnection. You can modify this behavior by overriding NetworkManager.OnServerAddPlayer. The default implementation of `OnServerAddPlayer` instantiates a new player instance from the player Prefab and calls NetworkServer.AddPlayerForConnection to spawn the new player instance. Your custom implementation of `OnServerAddPlayer` must also call `NetworkServer.AddPlayerForConnection`, but your are free to perform any other initialization you require in that method too.

The example below customizes the color of a player. First, add the color script to the player prefab:

```
using UnityEngine;
using Mirror;
class Player : NetworkBehaviour
{
    [SyncVar]
    public Color color;
}
```

Next, create a NetworkManager to handle spawning.

```
using UnityEngine;
using Mirror;

public class MyNetworkManager : NetworkManager
{
    public override void OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage)
    {
        GameObject player = (GameObject)Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        player.GetComponent<Player>().color = Color.red;
        NetworkServer.AddPlayerForConnection(conn, player);
    }
}
```

The function `NetworkServer.AddPlayerForConnection` does not have to be called from within `OnServerAddPlayer`. As long as the correct connection object and `playerControllerId` are passed in, it can be called after `OnServerAddPlayer` has returned. This allows asynchronous steps to happen in between, such as loading player data from a remote data source.

The system automatically spawns the player GameObject passed to `NetworkServer.AddPlayerForConnection` on the server, so you don’t need to call NetworkServer.Spawn for the player. Once a player is ready, the active networked GameObjects (that is, GameObjects with an associated NetworkIdentity) in the Scene spawn on the player’s client. All networked GameObjects in the game are created on that client with their latest state, so they are in sync with the other participants of the game.

You don’t need to use playerPrefab on the `NetworkManager` to create player GameObjects. You could use different methods of creating different players.

## Ready State

In addition to players, client connections also have a “ready” state. The host sends clients that are ready information about spawned GameObjects and state synchronization updates; clients which are not ready are not sent these updates. When a client initially connects to a server, it is not ready. While in this non-ready state, the client can do things that don’t require real-time interactions with the game state on the server, such as loading Scenes, allowing the player to choose an avatar, or fill in log-in boxes. Once a client has completed all its pre-game work, and all its Assets are loaded, it can call ClientScene.Ready to enter the “ready” state. The simple example above demonstrates implementation of ready states; because adding a player with `NetworkServer.AddPlayerForConnection` also puts the client into the ready state if it is not already in that state.

Clients can send and receive network messages without being ready, which also means they can do so without having an active player GameObject. So a client at a menu or selection screen can connect to the game and interact with it, even though they have no player GameObject. See documentation on Network messages for more details about sending messages without using commands and RPC calls.

## Switching Players

To replace the player GameObject for a connection, use NetworkServer.ReplacePlayerForConnection. This is useful for restricting the commands that players can issue at certain times, such as in a pre-game lobby screen. This function takes the same arguments as `AddPlayerForConnection`, but allows there to already be a player for that connection. The old player GameObject does not have to be destroyed. The NetworkLobbyManager uses this technique to switch from the NetworkLobbyPlayer GameObject to a gameplay player GameObject when all the players in the lobby are ready.

You can also use `ReplacePlayerForConnection` to respawn a player after their GameObject is destroyed. In some cases it is better to just disable a GameObject and reset its game attributes on respawn. The following code sample demonstrates how to actually replace the destroyed GameObject with a new GameObject:

```
class GameManager
{
    public void PlayerWasKilled(Player player)
    {
        var conn = player.connectionToClient;
        var newPlayer = Instantiate<GameObject>(playerPrefab);
        Destroy(player.gameObject);
        NetworkServer.ReplacePlayerForConnection(conn, newPlayer, 0);
    }
}
```

If the player GameObject for a connection is destroyed, then that client cannot execute Commands. They can, however, still send network messages.

To use `ReplacePlayerForConnection` you must have the NetworkConnection GameObject for the player’s client to establish the relationship between the GameObject and the client. This is usually the property connectionToClient on the NetworkBehaviour class, but if the old player has already been destroyed, then that might not be readily available.

To find the connection, there are some lists available. If using the `NetworkLobbyManager`, then the lobby players are available in lobbySlots. The NetworkServer also has lists of connections and localConnections.
