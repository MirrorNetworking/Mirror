# Application Events Overview

General description of Application Events

-   **OnStartHost**  
    This event is invoked when a host is started.
-   **OnStartServer**  
    This is invoked for NetworkBehaviour objects when they become active on the server.
    This could be triggered by NetworkServer.Listen() for objects in the Scene, or by NetworkServer.Spawn() for objects that are dynamically created.
    This will be called for objects on a "host" as well as for object on a dedicated server.
-   **OnStartClient**  
    Called on every NetworkBehaviour when it is activated on a client.
    Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.
-   **OnStartAuthority**  
    This is invoked on behaviours that have authority, based on context and NetworkIdentity.localPlayerAuthority.  
    This is called after OnStartServer and OnStartClient.  
    When NetworkIdentity.AssignClientAuthority is called on the server, this will be called on the client that owns the object. When an object is spawned with NetworkServer.SpawnWithClientAuthority, this will be called on the client that owns the object.
-   **OnStopHost**  
    This hook is called when a host is stopped.
-   **OnStopServer**  
    This event is called when a server is stopped - including when a host is stopped.
-   **OnStopClient**  
    This event is called when a client is stopped.
-   **OnStopAuthority**  
    This is invoked on behaviours when authority is removed.  
    When NetworkIdentity.RemoveClientAuthority is called on the server, this will be called on the client that owns the object.
-   **OnApplicationQuit**  
    Sent to all game objects before the application quits.
    In the editor this is called when the user stops playmode.
