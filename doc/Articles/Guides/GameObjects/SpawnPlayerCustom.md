# Custom Character Spawning

MirrorNG comes with a PlayerSpawner which will automatically spawn a player object when a client connects.

Many games need character customization. You may want to pick the color of the hair, eyes, skin, height, race, etc.

In this case,  you will need to create your own PlayerSpawner.  Follow these steps:

1) Create your player prefabs (as many as you need) and add them to the Spawnable Prefabs in your NetworkClient.
2) Create a message that describes your player. For example:
``` cs
public struct CreateMMOCharacterMessage
{
    public Race race;
    public string name;
    public Color hairColor;
    public Color eyeColor;
}

public enum Race
{
    None,
    Elvish,
    Dwarvish,
    Human
}
```
3) Create Player Spawner class and add it to some GameObject in your scene
``` cs
public class PlayerSpawner : MonoBehaviour
{
    public NetworkSceneManager SceneManager;
    public NetworkClient Client;
    public NetworkServer Server;
}
```
4) Drag the NetworkClient and NetworkServer and Scene manager to the fields
5) Hook into events:
```cs
public virtual void Start()
{
    Client.Authenticated.AddListener(OnClientAuthenticated);
    Server.Authenticated.AddListener(OnServerAuthenticated);
}
    ```
6) Send your message with your character data when your client connects, or after the user submits his preferences.
``` cs
// you can send the message here if you already know
// everything about the character at the time of connection
// or at a later time when the user submits his preferences
private void OnClientAuthenticated(INetworkConnection connection)
{
    sceneManager.SetClientReady();

    var mmoCharacter = new CreateMMOCharacterMessage {
        // populare the message with your data
    }
    connection.Send(mmoCharacter)
}
```
7) Receive your message in the server and spawn the player
```cs
private void OnServerAuthenticated(INetworkConnection connection)
{
    // wait for client to send us an AddPlayerMessage
    connection.RegisterHandler<CreateMMOCharacterMessage>(OnCreateCharacter);
}

void OnCreateCharacter(INetworkConnection conn, CreateMMOCharacterMessage msg)
{
    // create your player object
    // use the data in msg to configure it
    GameObject playerObject = ...;

    // spawn it as the player object
    server.AddPlayerForConnection(conn, playerObject);
}
```

## Ready State

In addition to players, client connections also have a “ready” state. The host sends clients that are ready information about spawned game objects and state synchronization updates; clients which are not ready are not sent these updates. When a client initially connects to a server, it is not ready. While in this non-ready state, the client can do things that don’t require real-time interactions with the game state on the server, such as loading Scenes, allowing the player to choose an avatar, or fill in log-in boxes. Once a client has completed all its pre-game work, and all its Assets are loaded, it can call `ClientScene.Ready` to enter the “ready” state. The simple example above demonstrates implementation of ready states; because adding a player with `NetworkServer.AddPlayerForConnection` also puts the client into the ready state if it is not already in that state.

Clients can send and receive network messages without being ready, which also means they can do so without having an active player game object. So a client at a menu or selection screen can connect to the game and interact with it, even though they have no player game object. See documentation on [Network Messages](../Communications/NetworkMessages.md) for more details about sending messages without using RPC calls.

Note the ready state may be going away in the future.

## Switching Players

To replace the player game object for a connection, use `NetworkServer.ReplacePlayerForConnection`. This is useful for restricting the Server RPC Calls that players can issue at certain times, such as in a pregame room screen. This function takes the same arguments as `AddPlayerForConnection`, but allows there to already be a player for that connection. The old player game object does not have to be destroyed. The `NetworkRoomManager` uses this technique to switch from the `NetworkRoomPlayer` game object to a game play player game object when all the players in the room are ready.

You can also use `ReplacePlayerForConnection` to respawn a player or change the object that represents the player. In some cases it is better to just disable a game object and reset its game attributes on respawn. The following code sample demonstrates how to actually replace the player game object with a new game object:

``` cs
public class MyNetworkManager : MonoBehaviour
{
    public NetworkServer Server;

    public void ReplacePlayer(NetworkConnection conn, GameObject newPrefab)
    {
        // Cache a reference to the current player object
        GameObject oldPlayer = conn.identity.gameObject;

        // Instantiate the new player object and broadcast to clients
        kServer.ReplacePlayerForConnection(conn, Instantiate(newPrefab));

        // Remove the previous player object that's now been replaced
        Server.Destroy(oldPlayer);
    }
}
```

If the player game object for a connection is destroyed, then that client cannot execute ServerRpc's. They can, however, still send network messages.

To use `ReplacePlayerForConnection` you must have the `NetworkConnection` game object for the player’s client to establish the relationship between the game object and the client. This is usually the property `connectionToClient` on the `NetworkBehaviour` class, but if the old player has already been destroyed, then that might not be readily available.

To find the connection, there are some lists available. If using the `NetworkRoomManager`, then the room players are available in `roomSlots`. The `NetworkServer` also has lists of `connections`.
