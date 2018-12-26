# NetworkClient

`NetworkClient` is a high-level API class that manages a network connection from a client to a server, and can send and receive messages between the client and the server. The `NetworkClient` class also helps to manage spawned network GameObjects, and routing of RPC message and network events.

See the [NetworkClient](#networkclient) script reference for more information.

## Properties

-   **serverIP**  
    The IP address of the server that this client is connected to.
-   **serverPort**  
    The port of the server that this client is connected to.
-   **connection**  
    The NetworkConnection GameObject this `NetworkClient` instance is using.
-   **handlers**  
    The set of registered message handler functions.
-   **numChannels**  
    The number of configured NetworkTransport QoS channels.
-   **isConnected**  
    True if the client is connected to a server.
-   **allClients**  
    List of active NetworkClients (static).
-   **active**  
    True if any NetworkClients are active (static).
