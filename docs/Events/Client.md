# Client Events Overview

General description of Client Events

-   **OnClientConnect**  
    Called on the server when a new client connects.
    Mirror calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.
-   **OnClientDisconnect**  
    Called on clients when disconnected from a server.
    This is called on the client when it disconnects from the server. Override this function to decide what happens when the client disconnects.
-   **OnClientNotReady**  
    Called on clients when a servers tells the client it is no longer ready.
    This is commonly used when switching Scenes.
-   **OnClientSceneChanged**  
    Called on clients when a Scene has completed loading, when the Scene load was initiated by the server.
    Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.
-   **OnClientError**  
    Called on clients when a network error occurs.
