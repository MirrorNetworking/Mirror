# Server Events Overview

General description of Server Events

-   **OnServerConnect**  
    Called on the server when a new client connects.  
    Miror calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.
-   **OnServerDisconnect**  
    Called on the server when a client disconnects.  
    This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.
-   **OnServerAddPlayer**  
    Called on the server when a client adds a new player with ClientScene.AddPlayer.  
    The default implementation for this function creates a new player object from the playerPrefab.
-   **OnServerRemovePlayer**  
    Called on the server when a client removes a player.  
    The default implementation of this function destroys the corresponding player object.  
    __Note__: OnServerRemovePlayer is only called if a RemovePlayer message is received, and a RemovePlayer message is only sent if ClientScene.RemovePlayer() is called. ClientScene.RemovePlayer() is never called by Mirror code. You must call it yourself to receive OnServerRemovePlayer in your NetworkManager.
-   **OnServerSceneChanged**  
    Called on the server when a Scene is completed loaded, when the Scene load was initiated by the server with ServerChangeScene().
-   **OnServerError**  
    Called on the server when a network error occurs for a client connection.
