# Customizing Network Visibility

The built-in [Network Proximity Checker](../Components/NetworkProximityChecker) component is the built-in default component for determining a game object’s network visibility. However, this only provides you with a distance-based check. Sometimes you might want to use other kinds of visibility check, such as grid-based rules, line-of-sight tests, navigation path tests, or any other type of test that suits your game.

To do this, you can implement your own custom equivalent of the Network Proximity Checker. To do that, you need to understand how the Network Proximity Checker works. See documentation on the Network Proximity Checker component.

The Network Proximity Checker is implemented using the public visibility interface of Mirror’s HLAPI. Using this same interface, you can implement any kind of visibility rules you desire. Each `NetworkIdentity` keeps track of the set of players that it is visible to. The players that a NetworkIdentity game object is visible to are called the “observers” of the NetworkIdentity.

The Network Proximity Checker calls the `RebuildObservers` method on the Network Identity component at a fixed interval (set using the “Vis Update Interval” value in the inspector), so that the set of network-visible game objects for each player is updated as they move around.

On the `NetworkBehaviour`class (which your networked scripts inherit from), there are some virtual functions for determining visibility. These are:

-   **OnCheckObserver**  
    This method is called on the server, on each networked game object when a new player enters the game. If it returns true, that player is added to the object’s observers. The Network Proximity Checker does a simple distance check in its implementation of this function, and uses `Physics.OverlapSphereNonAlloc` to find the players that are within the visibility distance for this object.

-   **OnRebuildObservers**  
    This method is called on the server when `RebuildObservers` is invoked. This method expects the set of observers to be populated with the players that can see the object. The NetworkServer then handles sending `ObjectHide` and `ObjectSpawn` messages based on the differences between the old and new visibility sets.

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
