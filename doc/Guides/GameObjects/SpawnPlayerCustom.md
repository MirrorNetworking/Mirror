# Custom Character Spawning

Many games need character customization. You may want to pick the color of the hair, eyes, skin, height, race, etc.

By default Mirror will instantiate the player for you. While that is convenient, it might prevent you from customizing it. Mirror provides the option of overriding player creation and customize it.

1) Create a class that extends `NetworkManager` if you have not done so. For example:

``` cs
public class MMONetworkManager : NetworkManager
{
    ...
}
```
and use it as your Network manager.

2) Open your Network Manager in the inspector and disable the "Auto Create Player" Boolean.

3) Create a message that describes your player. For example:

``` cs
public class CreateMMOCharacterMessage : MessageBase
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

4) Create your player prefabs (as many as you need) and add them to the "Register Spawnable Prefabs" in your Network Manager, or add a single prefab to the player prefab field in the inspector.

5) Send your message and register a player:

``` cs
public class MMONetworkManager : NetworkManager
{
    public override void OnStartServer()
    {
        base.OnStartServer();

        NetworkServer.RegisterHandler<CreateMMOCharacterMessage>(OnCreateCharacter);
    }

    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);

        // you can send the message here, or wherever else you want
        CreateMMOCharacterMessage characterMessage = new CreateMMOCharacterMessage
        {
            race = Race.Elvish,
            name = "Joe Gaba Gaba",
            hairColor = Color.red,
            eyeColor = Color.green
        };

        conn.Send(characterMessage);
    }

    void OnCreateCharacter(NetworkConnection conn, CreateMMOCharacterMessage message)
    {
        // playerPrefab is the one assigned in the inspector in Network
        // Manager but you can use different prefabs per race for example
        GameObject gameobject = Instantiate(playerPrefab);

        // Apply data from the message however appropriate for your game
        // Typically Player would be a component you write with syncvars or properties
        Player player = gameobject.GetComponent<Player>();
        player.hairColor = message.hairColor;
        player.eyeColor = message.eyeColor;
        player.name = message.name;
        player.race = message.race;

        // call this to use this gameobject as the primary controller
        NetworkServer.AddPlayerForConnection(conn, gameobject);
    }
}
```

## Ready State

In addition to players, client connections also have a “ready” state. The host sends clients that are ready information about spawned game objects and state synchronization updates; clients which are not ready are not sent these updates. When a client initially connects to a server, it is not ready. While in this non-ready state, the client can do things that don’t require real-time interactions with the game state on the server, such as loading Scenes, allowing the player to choose an avatar, or fill in log-in boxes. Once a client has completed all its pre-game work, and all its Assets are loaded, it can call `ClientScene.Ready` to enter the “ready” state. The simple example above demonstrates implementation of ready states; because adding a player with `NetworkServer.AddPlayerForConnection` also puts the client into the ready state if it is not already in that state.

Clients can send and receive network messages without being ready, which also means they can do so without having an active player game object. So a client at a menu or selection screen can connect to the game and interact with it, even though they have no player game object. See documentation on [Network Messages](../Communications/NetworkMessages.md) for more details about sending messages without using commands and RPC calls.

## Switching Players

To replace the player game object for a connection, use `NetworkServer.ReplacePlayerForConnection`. This is useful for restricting the commands that players can issue at certain times, such as in a pregame room screen. This function takes the same arguments as `AddPlayerForConnection`, but allows there to already be a player for that connection. The old player game object does not have to be destroyed. The `NetworkRoomManager` uses this technique to switch from the `NetworkRoomPlayer` game object to a game play player game object when all the players in the room are ready.

You can also use `ReplacePlayerForConnection` to respawn a player or change the object that represents the player. In some cases it is better to just disable a game object and reset its game attributes on respawn. The following code sample demonstrates how to actually replace the player game object with a new game object:

``` cs
public class MyNetworkManager : NetworkManager
{
    public void ReplacePlayer(GameObject newPrefab)
    {
        NetworkConnection conn = NetworkClient.connection;

        // Cache a reference to the current player object
        GameObject oldPlayer = conn.identity.gameObject;

        // Instantiate the new player object and broadcast to clients
        NetworkServer.ReplacePlayerForConnection(conn, Instantiate(newPrefab));

        // Remove the previous player object that's now been replaced
        NetworkServer.Destroy(oldPlayer);
    }
}
```

If the player game object for a connection is destroyed, then that client cannot execute Commands. They can, however, still send network messages.

To use `ReplacePlayerForConnection` you must have the `NetworkConnection` game object for the player’s client to establish the relationship between the game object and the client. This is usually the property `connectionToClient` on the `NetworkBehaviour` class, but if the old player has already been destroyed, then that might not be readily available.

To find the connection, there are some lists available. If using the `NetworkRoomManager`, then the room players are available in `roomSlots`. The `NetworkServer` also has lists of `connections`.
