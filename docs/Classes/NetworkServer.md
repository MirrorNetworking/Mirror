# NetworkServer

NetworkServer is a High-Level-API class that manages connections from multiple clients.

## Properties

-   **active**  
    Checks if the server has been started.
-   **connections**  
    A list of all the current connections from clients.
-   **dontListen**  
    If you enable this, the server will not listen for incoming connections on the regular network port.
-   **handlers**  
    Dictionary of the message handlers registered with the server.
-   **hostTopology**  
    The host topology that the server is using.
-   **localClientActive**  
    True if a local client is currently active on the server.
-   **localConnection**  
    The connection to the local client. This is only used when in host mode
