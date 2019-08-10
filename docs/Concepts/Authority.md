# Network Authority

Servers and clients can both manage a game object’s behavior. The concept of “authority” refers to how and where a game object is managed.

## Server Authority

The default state of authority in networked games using Mirror is that the Server has authority over all game objects which do not represent players. This means - for example - the server would manage control of all collectable items, moving platforms, NPCs - and any other parts of your game that players can interac and player game objects have authority on their owner’s client (meaning the client manages their behavior).

## Local Authority

Local authority (sometimes referred to as client authority) means the local client has authoritative control over a particular networked game object. This is in contrast to the default state which is that the server has authoritative control over networked game objects.

In addition to `isLocalPlayer`, you can choose to make the player game objects have “local authority”. This means that the player game object on its owner’s client is responsible for (or has authority over) itself. This is particularly useful for controlling movement; it means that each client has authority over how their own player game object is being controlled.

To enable local player authority on a game object, tick the Network Identity component’s Local Player Authority checkbox. The Network Transform component uses this “authority” setting, and sends movement information from the client to the other clients if this is set.

See Scripting API Reference documentation on NetworkIdentity and localPlayerAuthority for information on implementing local player authority via script.

Use the NetworkIdentity.hasAuthority property to find out whether a game object has local authority (also accessible on `NetworkBehaviour` for convenience). Non-player game objects have authority on the server, and player game objects with localPlayerAuthority set have authority on their owner’s client.

## Local (Client) Authority for Non-Player Game Objects

It is possible to have client authority over non-player game objects. There are two ways to do this. One is to spawn the game object using NetworkServer.SpawnWithClientAuthority, and pass the network connection of the client to take ownership. The other is to use NetworkIdentity.AssignClientAuthority with the network connection of the client to take ownership.

Assigning authority to a client causes Mirror to call OnStartAuthority() on each `NetworkBehaviour` on the game object, and sets the `hasAuthority` property to true. On other clients, the `hasAuthority` property remains false. Non-player game objects with client authority can send commands, just like players can. These commands are run on the server instance of the game object, not on the player associated with the connection.

If you want non-player game objects to have client authority, you must enable localPlayerAuthority on their Network Identity component. The example below spawns a game object and assigns authority to the client of the player that spawned it.

```cs
[Command]
void CmdSpawn()
{
    var go = Instantiate(otherPrefab, transform.position + new Vector3(0,1,0), Quaternion.identity);
    NetworkServer.SpawnWithClientAuthority(go, connectionToClient);
}
```


## Network Context Properties

The `NetworkBehaviour` class contains properties that allow scripts  
to know what the context of a networked game object is at any time.

-   isServer - true if the game object is on a server (or host) and has been spawned.
-   isClient - true if the game object is on a client, and was created by the server.
-   isLocalPlayer - true if the game object is a player game object for this client.
-   hasAuthority - true if the game object is owned by the local process

To see these properties, select the game object you want to inspect, and in the Inspector window, view the preview window for the NetworkBehaviour scripting components. You can use the value of these properties to execute code based on the context in which the script is running.
