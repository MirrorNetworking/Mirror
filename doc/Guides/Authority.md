# Network Authority

Servers and clients can both manage a game object’s behavior. The concept of “authority” refers to how and where a game object is managed.

## Server Authority

The default state of authority in networked games using Mirror is that the Server has authority over all game objects which do not represent players. This means, for example, the server would manage control of all collectible items, moving platforms, NPCs, and any other parts of your game that players can interact with, and player game objects have authority on their owner’s client (meaning the client manages their behavior).

## Client Authority

[![Client and server authority video tutorial](../images/video_tutorial.png)](https://www.youtube.com/watch?v=WBFrA0Gnpi8&list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP&index=4)

Client authority means the local client can control a networked game object. By default only the server has control over a networked object.

In practical terms, having client authority means that the client can call [Command](Communications/RemoteActions.md) methods, and if the client disconnects, the object is automatically destroyed.

Use the `NetworkIdentity.hasAuthority` property in the client to find out whether a game object has local authority (also accessible on `NetworkBehaviour` for convenience).

Assigning authority to a client causes Mirror to call `OnStartAuthority()` on each `NetworkBehaviour` on the game object on the authority client, and sets the `hasAuthority` property to true. On other clients, the `hasAuthority` property remains false.

Player objects always have client authority. This is required for controlling movement and other player actions.

**Client Authority is not to be confused with client authoritative architecture** Any action must still go to the server via a [Command](Communications/RemoteActions.md). The client cannot modify SyncVars or affect other clients directly

## Non-Player Game Objects

It is possible to have client authority over non-player game objects. There are two ways to do this. One is to spawn the game object using `NetworkServer.Spawn` and pass the network connection of the client to take ownership. The other is to use `NetworkIdentity.AssignClientAuthority` with the network connection of the client to take ownership.

The example below spawns a game object and assigns authority to the client of the player that spawned it.

``` cs
[Command]
void CmdSpawn()
{
    GameObject go = Instantiate(otherPrefab, transform.position + new Vector3(0,1,0), Quaternion.identity);
    NetworkServer.Spawn(go, connectionToClient);
}
```

## Network Context Properties

The `NetworkBehaviour` class contains properties that allow scripts to know what the context of a networked game object is at any time.

-   **isServer**: true if the game object is on a server and has been spawned.
-   **isClient**: true if the game object is on a client, and was created by the server.
-   **isLocalPlayer**: true if the game object is a player game object for this client.
-   **hasAuthority**: true if the game object is owned by this client.

On the server, the `NetworkIdentity` holds the owning client in `connectionToClient`.

To see these properties, select the game object you want to inspect, and in the Inspector window, view the preview window for the NetworkBehaviour scripting components. You can use the value of these properties to execute code based on the context in which the script is running.
