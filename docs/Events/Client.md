# Client Events Overview

General description of Client Events

-   **OnClientConnect**  
    Called on the client when it connects.
    Mirror calls this on the client when it connects to the Server. Use an override to tell the NetworkManager what to do when the client connects to a server.
-   **OnStartLocalPlayer**  
    Called when the local player object has been set up. This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.
-   **OnClientDisconnect**  
    Called on clients when disconnected from a server.
    This is called on the client when it disconnects from the server. Override this function to decide what happens when the client disconnects.
-   **OnClientNotReady**  
    Called on clients when a servers tells the client it is no longer ready.
    This is commonly used when switching Scenes.
-   **OnClientChangeScene**  
    Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed.  
    This allows client to do work / cleanup / prep before the scene changes.
-   **OnClientSceneChanged**  
    Called on clients when a Scene has completed loading, when the Scene load was initiated by the server.
    Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.
-   **OnClientError**  
    Called on clients when a network error occurs.
