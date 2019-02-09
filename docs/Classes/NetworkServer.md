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
-   **listenPort**  
    The port that the server is listening on.
-   **localClientActive**  
    True if a local client is currently active on the server.
-   **localConnection**  
    The connection to the local client. This is only used when in host mode
-   **maxDelay**  
    The maximum delay before sending packets on connections.
-   **networkConnectionClass**  
    The class to be used when creating new network connections.
-   **numChannels**  
    The number of channels the network is configure with.
-   **serverHostId**  
    The transport layer hostId used by this server.
-   **useWebSockets**  
    This makes the server listen for WebSockets connections instead of normal transport layer connections.
