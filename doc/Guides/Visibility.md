# Network Visibility

Multiplayer games use the concept of network visibility to determine which players can see which game objects at any given time during game play. In a game that has a moving viewpoint and moving game objects, it’s common that players cannot see everything that is happening in the game at once.

If a particular player, at a certain point in time during game play, cannot see most of the other players, non-player characters, or other moving or interactive things in your game, there is usually no need for the server to send information about those things to the player’s client.

This can benefit your game in two ways:
-   It reduces the amount of data sent across the network between players. This can help improve the responsiveness of your game, and reduce bandwidth use. The bigger and more complex your multiplayer game, the more important this issue is.
-   It also helps prevent some cheating. Since a player client does not have information about things that can’t be seen, a hack on that player’s computer cannot reveal the information.

The idea of “visibility” in the context of networking doesn’t necessarily relate to whether game objects are directly visible on-screen. Instead, it relates to whether data should or shouldn’t be sent about the game object in question to a particular client. Put simply, if a client can’t ‘see’ an game object, it does not need to be sent information about that game object across the network. Ideally you want to limit the amount of data you are sending across the network to only what is necessary, because sending large amounts of unnecessary data across the network can cause network performance problems.

However, it can be also be resource intensive or complex to determine accurately whether a game object truly visible to a given player, so it’s often a good idea to use a more simple calculation for the purposes of determining whether a player should be sent networked data about it - i.e. whether it is ‘Network Visible’. The balance you want to achieve when considering this is between the cost of the complexity of the calculation for determining the visibility, and the cost of sending more information than necessary over the network. A very simple way to calculate this is a distance (proximity) check, and Mirror provides a built-in component for this purpose.

## Network Proximity Checker Component

Mirror’s [Network Proximity Checker](../Components/NetworkProximityChecker.md) component is simplest way to implement network visibility for players. It works in conjunction with the physics system to determine whether game objects are close enough (that is, “visible” for the purposes of sending network messages in your multiplayer game).

## Network Scene Checker Component

Mirror's [Network Scene Checker](../Components/NetworkSceneChecker.md) component can be used to isolate players and networked objects on the server in additive scene instances.

## Network Visibility on Remote Clients

When a player on a remote client joins a networked game, only game objects that are network-visible to the player will be spawned on that remote client. This means that even if the player enters a large world with many networked game objects, the game can start quickly because it does not need to spawn every game object that exists in the world. Note that this applies to networked game objects in your Scene, but does not affect the loading of Assets. Unity still takes time to load the Assets for registered Prefabs and Scene game objects.

When a player moves within the world, the set of network-visible game objects changes. The player’s client is told about these changes as they happen. The `ObjectHide` message is sent to clients when a game object becomes no longer network-visible. By default, Mirror destroys the game object when it receives this message. When a game object becomes visible, the client receives an `ObjectSpawn` message, as if Mirror has spawned the game object for the first time. By default, the game object is instantiated like any other spawned game object.

## Network Visibility on the Host

The host shares the same Scene as the server, because it acts as both the server and the client to the player hosting the game. For this reason, it cannot destroy game objects that are not visible to the local player.

Instead, there is the virtual method OnSetLocalVisibility in the NetworkVisibility class that is invoked. This method is invoked on all scripts that inherit from `NetworkVisibility` on game objects that change visibility state on the host.

The default implementation of `OnSetLocalVisibility` disables or enables all renderer components on the game object. If you want to customize this implementation, you can override the method in your script, and provide a new behavior for how the host (and therefore the local client) should respond when a game object becomes network-visible or invisible (such as disabling HUD elements or renderers).

## Customizing Network Visibility

Sometimes you might want to use other kinds of visibility check, such as grid-based rules, line-of-sight tests, navigation path tests, or any other type of test that suits your game.

To do this, you can create your own custom Network Observer from a [script template](../General/ScriptTemplates.md) via the Assets menu by clicking Create -> Mirror -> Network Observer.

It may be helpful to understand how the Network Proximity Checker works.

The Network Proximity Checker is implemented using the public visibility interface of Mirror’s HLAPI. Using this same interface, you can implement any kind of visibility rules you desire. Each `NetworkIdentity` keeps track of the set of players that it is visible to. The players that a NetworkIdentity game object is visible to are called the “observers” of the NetworkIdentity.

The Network Proximity Checker calls the `RebuildObservers` method on the Network Identity component at a fixed interval (set using the “Vis Update Interval” value in the inspector), so that the set of network-visible game objects for each player is updated as they move around.

In the `NetworkVisibility` class (which your custom observer scripts inherit from), there are some virtual functions for determining visibility. These are:
-   **OnCheckObserver**  
    This method is called on the server, on each networked game object when a new player enters the game. If it returns true, that player is added to the object’s observers. The Network Proximity Checker does a simple distance check in its implementation of this function, and uses `Physics.OverlapSphereNonAlloc` to find the players that are within the visibility distance for this object.
-   **OnRebuildObservers**  
    This method is called on the server when `RebuildObservers` is invoked. This method expects the set of observers to be populated with the players that can see the object. The NetworkServer then handles sending `ObjectHide` and `ObjectSpawn` messages based on the differences between the old and new visibility sets.
-   **OnSetHostVisibility**  
    This method is called on the server by the visibility system for objects on a host.  Objects on a host (with a local client) cannot be disabled or destroyed when they are not visibile to the local client. So this function is called to allow custom code to hide these objects. A typical implementation will disable renderer components on the object. This is only called on local clients on a host.

You can check whether any given networked game object is a player by checking if its `NetworkIdentity` has a valid connectionToClient. For example:

``` cs
int hitCount = Physics.OverlapSphereNonAlloc(transform.position, visRange, hitsBuffer3D, castLayers);

for (int i = 0; i < hitCount; i++)
{
    Collider hit = hitsBuffer3D[i];

    NetworkIdentity identity = hit.GetComponent<NetworkIdentity>();

    if (identity != null && identity.connectionToClient != null)
        observers.Add(identity.connectionToClient);
}
```
