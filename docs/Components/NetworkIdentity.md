# NetworkIdentity

The Network Identity component is at the heart of the Unity networking high-level API. It controls a game object’s unique identity on the network, and it uses that identity to make the networking system aware of the game object. It offers two different options for configuration and they are mutually exclusive, which means either one of the options or none can be checked.

-   **Server Only**  
    Tick this checkbox to ensure that Unity only spawns the game object on the server, and not on clients.
-   **Local Player Authority**  
    Tick this checkbox to give authoritative network control of this game object to the client that owns it. The player game object on that client has authority over it. Other components such as Network Transform use this to determine which client to treat as the source of authority.

If none of these options is checked, the server will have authority over the object. Changes made by clients (e.g. moving the object) are not allowed and will not be synchronized.

![Inspector](NetworkIdentity.jpg)

## Instantiated Network Game Objects

With the Unity’s server-authoritative networking system, the server must spawn networked game objects with network identities, using `NetworkServer.Spawn`. This automatically creates them on clients that are connected to the server, and assigns them a `netId`.

You must put a Network Identity component on any Prefabs that spawn at runtime for the network system to use them. See [Object Spawning](../Concepts/GameObjects/SpawnObject) for more information.

## Scene-based Network Game Objects

You can also network game objects that are saved as part of your Scene (for example, environmental props). Networking game objects makes them behave slightly differently, because you need to have them spawn across the network.

When building your game, Unity disables all Scene-based game objects with Network Identity components. When a client connects to the server, the server sends spawn messages to tell the client which Scene game objects to enable and what their most up-to-date state information is. This ensures the client’s game does not contain game objects at incorrect locations when they start playing, or that Unity does not spawn and immediately destroy game objects on connection (for example, if an event removed the game object before that client connected). See [Networked Scene Game Objects](../Concepts/GameObjects/SceneObjects) for more information.

## Preview Pane Information

This component contains network tracking information, and displays that information in the preview pane. For example, the scene ID, network ID and asset ID the object has been assigned. This allows you to inspect the information which can be useful for investigation and debugging.

![Preview](NetworkIdentityPreview.png)

At runtime there is more information to display here (a disabled NetworkBehaviour is displayed non-bold):

![Runtime Preview](NetworkIdentityPreviewRuntime.png)

## Properties

-   **assetId**  
    This identifies the prefab associated with this object (for spawning).
-   **clientAuthorityOwner**  
    The client that has authority for this object. This will be null if no client has authority.
-   **connectionToClient**  
    The NetworkConnection associated with this NetworkIdentity. This is only valid for player objects on the server.
-   **connectionToServer**  
    The NetworkConnection associated with this NetworkIdentity. This is only valid for player objects on a local client.
-   **hasAuthority**  
    True if this object is the authoritative version of the object. This would mean either on a the server for normal objects, or on the client with localPlayerAuthority.
-   **isClient**  
    True if this object is running on a client.
-   **isLocalPlayer**  
    This returns true if this object is the one that represents the player on the local machine.
-   **isServer**  
    True if this object is running on the server, and has been spawned.
-   **localPlayerAuthority**  
    True if this object is controlled by the client that owns it - the local player object on that client has authority over it. This is used by other components such as NetworkTransform.
-   **netId**  
    A unique identifier for this network session, assigned when spawned.
-   **NetworkBehaviours**  
    Cached array of Network Behaviors on this object.
-   **observers**  
    The list of client NetworkConnections that are able to see this object. This is read-only.
-   **sceneId**  
    A unique identifier for networked objects in a Scene. This is only populated in play-mode.
-   **spawned**  
    Dictionary of all spawned NetworkIdentities by netId. This is read-only.
-   **serverOnly**  
    A flag to make this object not be spawned on clients.

## Methods

-   **AssignClientAuthority**  
    This assigns control of an object to a client via the client's NetworkConnection.
-   **RebuildObservers**  
    This causes the set of players that can see this object to be rebuild. The OnRebuildObservers callback function will be invoked on each NetworkBehaviour.
-   **RemoveClientAuthority**  
    Removes ownership for an object for a client by its conneciton.  
    This applies to objects that had authority set by AssignClientAuthority or NetworkServer.SpawnWithClientAuthority.  
    Authority cannot be removed for player objects.
-   **ResetNextNetworkId**  
    Sets the next netId back to 1. This is called from `NetworkServer.Shutdown`
